using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Auth.API.Services;

public interface IPasskeyService
{
    Task<CredentialCreateOptions> GetRegistrationOptionsAsync(
        Guid userId,
        string userName,
        string displayName,
        CancellationToken cancellationToken = default);

    Task<PasskeyRegisterResult> CompleteRegistrationAsync(
        Guid userId,
        AuthenticatorAttestationRawResponse attestation,
        string friendlyName,
        CancellationToken cancellationToken = default);

    Task<AssertionOptions> GetAssertionOptionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AssertionOptions> GetPasswordlessLoginOptionsAsync(CancellationToken cancellationToken = default);

    Task<PasskeyAuthResult> CompleteAssertionAsync(
        AuthenticatorAssertionRawResponse assertion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PasskeyListItem>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid credentialRowId, CancellationToken cancellationToken = default);
}

public enum PasskeyRegisterResult
{
    Success,
    VerificationFailed,
}

public enum PasskeyAuthResult
{
    Success,
    VerificationFailed,
    NotFound,
}

public sealed record PasskeyListItem(Guid Id, string FriendlyName, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc);
