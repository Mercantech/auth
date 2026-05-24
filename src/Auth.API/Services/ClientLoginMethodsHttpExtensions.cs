using Auth.API.Hosting;
using Auth.API.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public static class ClientLoginMethodsHttpExtensions
{
    public static ClientLoginMethodsPolicy GetLoginMethods(this HttpContext http) =>
        http.GetLoginBranding()?.LoginMethods
        ?? ClientLoginMethodsPolicy.FromGlobalConfiguration(
            http.RequestServices.GetRequiredService<IConfiguration>(),
            http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value);

    public static async Task<ClientLoginMethodsPolicy> ResolveLoginMethodsAsync(
        HttpContext http,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var branding = http.GetLoginBranding();
        if (branding is not null)
            return branding.LoginMethods;

        var svc = http.RequestServices.GetRequiredService<IClientLoginBrandingService>();
        var clientId = LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(returnUrl, http);
        var resolved = await svc.ResolveAsync(
            http,
            returnUrl,
            string.IsNullOrEmpty(clientId) ? null : clientId,
            cancellationToken);
        return resolved.LoginMethods;
    }

    public static async Task<bool> IsLoginMethodAllowedAsync(
        HttpContext http,
        string methodId,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var policy = await ResolveLoginMethodsAsync(http, returnUrl, cancellationToken);
        return policy.IsAllowed(methodId);
    }
}
