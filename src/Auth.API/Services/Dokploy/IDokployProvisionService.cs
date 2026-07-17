using Auth.API.Models.Entities;

namespace Auth.API.Services.Dokploy;

public interface IDokployProvisionService
{
    /// <summary>
    /// Best-effort: opretter/linker Dokploy-bruger hvis Enabled og wantDokploy.
    /// Fejl logges og gemmes — kaster ikke (Auth-signup skal ikke fejle).
    /// </summary>
    Task TryProvisionIfRequestedAsync(
        User user,
        bool wantDokploy,
        CancellationToken cancellationToken = default);
}
