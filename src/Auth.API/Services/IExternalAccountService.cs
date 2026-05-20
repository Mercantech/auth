using System.Security.Claims;
using Auth.API.Models;

namespace Auth.API.Services;

public interface IExternalAccountService
{
    Task<Guid> FindOrLinkUserAsync(
        ClaimsPrincipal externalPrincipal,
        string provider,
        UserEmailKind emailKind,
        CancellationToken cancellationToken = default);

    /// <summary>Tilføj ekstern udbyder-identitet til den angivne bruger (authentificeret account-link flow).</summary>
    Task<LinkExternalOutcome> LinkExternalToUserAsync(
        Guid targetUserId,
        ClaimsPrincipal externalPrincipal,
        string provider,
        UserEmailKind emailKind,
        CancellationToken cancellationToken = default);

    /// <summary>Fjern ekstern tilknytning. Kræver mindst én tilbageværende login-metode (andre ExternalLogins eller LocalLogin).</summary>
    Task<UnlinkExternalLoginResult> UnlinkExternalLoginAsync(
        Guid currentUserId,
        Guid externalLoginId,
        CancellationToken cancellationToken = default);
}
