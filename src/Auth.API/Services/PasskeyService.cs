using Auth.API.Data;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Auth.API.Services;

public class PasskeyService(
    AuthDbContext db,
    IFido2 fido2,
    IMemoryCache cache,
    TimeProvider time) : IPasskeyService
{
    private static string RegCacheKey(Guid userId) => $"fido2:reg:{userId:D}";
    private static string AssertCacheKey(Guid userId) => $"fido2:assert:{userId:D}";
    private const string PasswordlessCacheKey = "fido2:assert:passwordless";

    public async Task<CredentialCreateOptions> GetRegistrationOptionsAsync(
        Guid userId,
        string userName,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.UserPasskeyCredentials
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync(cancellationToken);

        var user = new Fido2User
        {
            Id = userId.ToByteArray(),
            Name = userName,
            DisplayName = displayName,
        };

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = existing,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
        });

        cache.Set(RegCacheKey(userId), options, TimeSpan.FromMinutes(5));
        return options;
    }

    public async Task<PasskeyRegisterResult> CompleteRegistrationAsync(
        Guid userId,
        AuthenticatorAttestationRawResponse attestation,
        string friendlyName,
        CancellationToken cancellationToken = default)
    {
        if (!cache.TryGetValue(RegCacheKey(userId), out CredentialCreateOptions? options) || options is null)
            return PasskeyRegisterResult.VerificationFailed;

        try
        {
            var result = await fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = attestation,
                    OriginalOptions = options,
                    IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                        !await db.UserPasskeyCredentials.AnyAsync(c => c.CredentialId == args.CredentialId, ct),
                },
                cancellationToken);

            if (result.PublicKey is null || result.PublicKey.Length == 0 || result.Id is null || result.Id.Length == 0)
                return PasskeyRegisterResult.VerificationFailed;

            var now = time.GetUtcNow().UtcDateTime;
            var name = string.IsNullOrWhiteSpace(friendlyName) ? "Passkey" : friendlyName.Trim();
            if (name.Length > 80)
                name = name[..80];

            db.UserPasskeyCredentials.Add(new UserPasskeyCredential
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CredentialId = result.Id,
                PublicKey = result.PublicKey,
                SignCount = result.SignCount,
                AaGuid = result.AaGuid,
                FriendlyName = name,
                CreatedAtUtc = now,
            });

            await db.SaveChangesAsync(cancellationToken);
            cache.Remove(RegCacheKey(userId));
            return PasskeyRegisterResult.Success;
        }
        catch
        {
            return PasskeyRegisterResult.VerificationFailed;
        }
    }

    public async Task<AssertionOptions> GetAssertionOptionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var creds = await db.UserPasskeyCredentials
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync(cancellationToken);

        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = creds,
            UserVerification = UserVerificationRequirement.Required,
        });

        cache.Set(AssertCacheKey(userId), options, TimeSpan.FromMinutes(5));
        return options;
    }

    public Task<AssertionOptions> GetPasswordlessLoginOptionsAsync(CancellationToken cancellationToken = default)
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });

        cache.Set(PasswordlessCacheKey, options, TimeSpan.FromMinutes(5));
        return Task.FromResult(options);
    }

    public async Task<PasskeyAuthResult> CompleteAssertionAsync(
        AuthenticatorAssertionRawResponse assertion,
        CancellationToken cancellationToken = default)
    {
        if (assertion.RawId is null || assertion.RawId.Length == 0)
            return PasskeyAuthResult.NotFound;

        var stored = await db.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == assertion.RawId, cancellationToken);
        if (stored is null)
            return PasskeyAuthResult.NotFound;

        var cacheKey = cache.TryGetValue(AssertCacheKey(stored.UserId), out AssertionOptions? userOpts) && userOpts is not null
            ? AssertCacheKey(stored.UserId)
            : PasswordlessCacheKey;

        if (!cache.TryGetValue(cacheKey, out AssertionOptions? options) || options is null)
            return PasskeyAuthResult.VerificationFailed;

        try
        {
            var result = await fido2.MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = assertion,
                    OriginalOptions = options,
                    StoredPublicKey = stored.PublicKey,
                    StoredSignatureCounter = stored.SignCount,
                    IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) =>
                    {
                        var row = await db.UserPasskeyCredentials
                            .AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CredentialId == args.CredentialId, ct);
                        if (row is null)
                            return false;
                        if (args.UserHandle is null || args.UserHandle.Length == 0)
                            return row.UserId == stored.UserId;
                        return row.UserId.ToByteArray().SequenceEqual(args.UserHandle);
                    },
                },
                cancellationToken);

            stored.SignCount = result.SignCount;
            stored.LastUsedAtUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);

            cache.Remove(AssertCacheKey(stored.UserId));
            cache.Remove(PasswordlessCacheKey);
            return PasskeyAuthResult.Success;
        }
        catch
        {
            return PasskeyAuthResult.VerificationFailed;
        }
    }

    public async Task<IReadOnlyList<PasskeyListItem>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await db.UserPasskeyCredentials
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new PasskeyListItem(c.Id, c.FriendlyName, c.CreatedAtUtc, c.LastUsedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<string?> DeleteAsync(
        Guid userId,
        Guid credentialRowId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialRowId && c.UserId == userId, cancellationToken);
        if (row is null)
            return null;

        var name = row.FriendlyName;
        db.UserPasskeyCredentials.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return name;
    }
}
