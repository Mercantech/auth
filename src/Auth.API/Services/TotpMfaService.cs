using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OtpNet;

namespace Auth.API.Services;

public class TotpMfaService(
    AuthDbContext db,
    TotpSecretProtector protector,
    TimeProvider time,
    IOptions<MfaOptions> mfaOptions) : ITotpMfaService
{
    public async Task<TotpSetupResult> BeginSetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        var base32 = Base32Encoding.ToString(key);
        var cipher = protector.Protect(base32);

        var row = await db.UserTotpMfas.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        if (row is null)
        {
            db.UserTotpMfas.Add(new UserTotpMfa
            {
                UserId = userId,
                SecretCipher = cipher,
                IsEnabled = false,
            });
        }
        else
        {
            row.SecretCipher = cipher;
            row.IsEnabled = false;
            row.EnabledAtUtc = null;
            await db.UserMfaRecoveryCodes
                .Where(c => c.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var user = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.DisplayName })
            .FirstAsync(cancellationToken);

        var label = Uri.EscapeDataString(user.Email ?? user.DisplayName);
        var issuer = Uri.EscapeDataString(mfaOptions.Value.IssuerName);
        var uri = $"otpauth://totp/{issuer}:{label}?secret={base32}&issuer={issuer}&digits=6";

        return new TotpSetupResult(base32, uri);
    }

    public async Task<TotpConfirmResult> ConfirmSetupAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var row = await db.UserTotpMfas.FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
        if (row is null || row.IsEnabled)
            return TotpConfirmResult.NotPending;

        if (!VerifyTotp(row.SecretCipher, code))
            return TotpConfirmResult.InvalidCode;

        var now = time.GetUtcNow().UtcDateTime;
        row.IsEnabled = true;
        row.EnabledAtUtc = now;

        var plainCodes = GenerateRecoveryCodes(mfaOptions.Value.RecoveryCodeCount);
        foreach (var plain in plainCodes)
        {
            db.UserMfaRecoveryCodes.Add(new UserMfaRecoveryCode
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(NormalizeRecovery(plain)),
                CreatedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return TotpConfirmResult.Success;
    }

    public async Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var row = await db.UserTotpMfas.AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == userId && t.IsEnabled, cancellationToken);
        if (row is null)
            return false;

        return VerifyTotp(row.SecretCipher, code);
    }

    public async Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeRecovery(code);
        var rows = await db.UserMfaRecoveryCodes
            .Where(c => c.UserId == userId && c.UsedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            if (!BCrypt.Net.BCrypt.Verify(norm, row.CodeHash))
                continue;

            row.UsedAtUtc = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<bool> DisableAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default)
    {
        if (!await VerifyCodeAsync(userId, totpCode, cancellationToken))
            return false;

        await db.UserMfaRecoveryCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.UserTotpMfas.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    public Task<bool> IsEnabledAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.UserTotpMfas.AsNoTracking()
            .AnyAsync(t => t.UserId == userId && t.IsEnabled, cancellationToken);

    private bool VerifyTotp(string secretCipher, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var secret = protector.Unprotect(secretCipher);
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(
            code.Trim().Replace(" ", "", StringComparison.Ordinal),
            out _,
            new VerificationWindow(previous: 1, future: 1));
    }

    private static IReadOnlyList<string> GenerateRecoveryCodes(int count)
    {
        var list = new List<string>(count);
        for (var i = 0; i < count; i++)
            list.Add(CreateRecoveryCode());
        return list;
    }

    /// <summary>10 tegn, store bogstaver + tal (uden 0/O/1/I for læsbarhed).</summary>
    private static string CreateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[10];
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(10);
        for (var i = 0; i < 10; i++)
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        return new string(chars);
    }

    private static string NormalizeRecovery(string code) =>
        code.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal).ToUpperInvariant();
}
