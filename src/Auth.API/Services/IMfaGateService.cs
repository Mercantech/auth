namespace Auth.API.Services;

public interface IMfaGateService
{
    Task<bool> HasSecondFactorConfiguredAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Skal brugeren til /Account/Mfa efter primær login?</summary>
    Task<bool> RequiresMfaStepAsync(
        Guid userId,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default);

    Task<bool> RoleRequiresMfaSetupAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default);
}
