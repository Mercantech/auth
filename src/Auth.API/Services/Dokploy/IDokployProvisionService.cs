using Auth.API.Models.Entities;

namespace Auth.API.Services.Dokploy;

public enum DokployProvisionStatus
{
    Skipped,
    Disabled,
    MissingEmail,
    InvalidPassword,
    AlreadyProvisioned,
    LinkedExisting,
    Created,
    PasswordReset,
    Failed,
}

public sealed record DokployProvisionResult(
    DokployProvisionStatus Status,
    string? DokployUserId = null,
    string? ErrorMessage = null);

public interface IDokployProvisionService
{
    /// <summary>
    /// Best-effort ved signup: opretter/linker Dokploy-bruger med den angivne adgangskode.
    /// </summary>
    Task TryProvisionIfRequestedAsync(
        User user,
        bool wantDokploy,
        string? dokployPassword,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Self-service: opretter Dokploy-bruger med <paramref name="password"/> (min. 8 tegn).
    /// Ingen invite — kun <c>user.createUserWithCredentials</c>.
    /// </summary>
    Task<DokployProvisionResult> ProvisionAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nulstiller Dokploy-adgangskode (slet + genopret bruger, genanvender Auth-ACL).
    /// </summary>
    Task<DokployProvisionResult> ResetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default);
}
