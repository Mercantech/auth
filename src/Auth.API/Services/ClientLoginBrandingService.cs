using Auth.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class ClientLoginBrandingService(AuthDbContext db) : IClientLoginBrandingService
{
    public async Task<LoginBrandingContext> ResolveAsync(
        HttpContext http,
        string? returnUrl,
        string? clientIdFromQuery,
        CancellationToken cancellationToken = default)
    {
        var isOAuth = IsOAuthReturnUrl(returnUrl);
        var clientId = ResolveClientId(clientIdFromQuery, returnUrl, http, isOAuth);

        if (string.IsNullOrEmpty(clientId))
            return new LoginBrandingContext(LoginThemeCatalog.Mercantec, null, isOAuth);

        var clientIdNorm = clientId.Trim();
        var app = await db.ClientApps
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.IsActive && EF.Functions.ILike(c.ClientId, clientIdNorm),
                cancellationToken);

        if (app is null)
            return new LoginBrandingContext(LoginThemeCatalog.Mercantec, clientId, isOAuth);

        var theme = LoginThemeCatalog.ResolveForClient(app.LoginThemeId, clientId);
        return new LoginBrandingContext(theme, clientId, isOAuth);
    }

    public void SetClientCookie(HttpContext http, string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return;

        http.Response.Cookies.Append(
            LoginBrandingConstants.ClientCookieName,
            clientId.Trim(),
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = http.Request.IsHttps,
                MaxAge = LoginBrandingConstants.ClientCookieLifetime,
                Path = "/",
            });
    }

    public void ClearClientCookie(HttpContext http) =>
        http.Response.Cookies.Delete(LoginBrandingConstants.ClientCookieName, new CookieOptions { Path = "/" });

    internal static bool IsOAuthReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        var path = returnUrl;
        var qIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (qIndex >= 0)
            path = returnUrl[..qIndex];

        return path.StartsWith("/oauth/authorize", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? TryParseClientIdFromReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;

        var qIndex = returnUrl.IndexOf('?', StringComparison.Ordinal);
        if (qIndex < 0)
            return null;

        var query = returnUrl[(qIndex + 1)..];
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("client_id=", StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(part["client_id=".Length..]);
        }

        return null;
    }

    private static string? ResolveClientId(
        string? clientIdFromQuery,
        string? returnUrl,
        HttpContext http,
        bool isOAuth)
    {
        if (!string.IsNullOrWhiteSpace(clientIdFromQuery))
            return clientIdFromQuery.Trim();

        var fromReturn = TryParseClientIdFromReturnUrl(returnUrl);
        if (!string.IsNullOrWhiteSpace(fromReturn))
            return fromReturn;

        if (!isOAuth)
            return null;

        return http.Request.Cookies.TryGetValue(LoginBrandingConstants.ClientCookieName, out var cookie)
            && !string.IsNullOrWhiteSpace(cookie)
            ? cookie.Trim()
            : null;
    }
}
