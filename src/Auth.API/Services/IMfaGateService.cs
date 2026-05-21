namespace Auth.API.Services;

public interface IMfaGateService
{
    Task<bool> HasSecondFactorConfiguredAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Skal brugeren til /Account/Mfa efter primær login?</summary>
    /// <param name="primaryLoginMethod">Fx <see cref="Security.MercantecAuthClaims.LoginMethodValues.Passkey"/> — passkey-login springer MFA over.</param>
    Task<bool> RequiresMfaStepAsync(
        Guid userId,
        IReadOnlyList<string> roleNames,
        string? primaryLoginMethod = null,
        CancellationToken cancellationToken = default);

    Task<bool> RoleRequiresMfaSetupAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default);
}
