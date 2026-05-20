namespace Auth.API.Services;

public interface ITotpMfaService
{
    Task<TotpSetupResult> BeginSetupAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TotpConfirmResult> ConfirmSetupAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<bool> TryConsumeRecoveryCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default);

    Task<bool> DisableAsync(Guid userId, string totpCode, CancellationToken cancellationToken = default);

    Task<bool> IsEnabledAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed record TotpSetupResult(string SharedSecretBase32, string AuthenticatorUri);

public enum TotpConfirmResult
{
    Success,
    InvalidCode,
    NotPending,
}
