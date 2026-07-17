using Auth.API.Models.Entities;

namespace Auth.API.Services.Dokploy;

public enum DokployProvisionStatus
{
    Skipped,
    Disabled,
    MissingEmail,
    AlreadyProvisioned,
    LinkedExisting,
    InvitedOrCreated,
    Failed,
}

public sealed record DokployProvisionResult(
    DokployProvisionStatus Status,
    string? DokployUserId = null,
    string? ErrorMessage = null);

public interface IDokployProvisionService
{
    /// <summary>
    /// Best-effort ved signup: opretter/linker Dokploy-bruger hvis Enabled og wantDokploy.
    /// Fejl logges — kaster ikke.
    /// </summary>
    Task TryProvisionIfRequestedAsync(
        User user,
        bool wantDokploy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Self-service / eksplicit provision. Returnerer status til UI.
    /// </summary>
    Task<DokployProvisionResult> ProvisionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
