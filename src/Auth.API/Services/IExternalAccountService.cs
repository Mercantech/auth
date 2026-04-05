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
}
