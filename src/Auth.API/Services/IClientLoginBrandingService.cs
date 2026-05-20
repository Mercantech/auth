namespace Auth.API.Services;

public interface IClientLoginBrandingService
{
    /// <summary>Resolver tema for OAuth login-flow (Login/Register/Mfa).</summary>
    Task<LoginBrandingContext> ResolveAsync(
        HttpContext http,
        string? returnUrl,
        string? clientIdFromQuery,
        CancellationToken cancellationToken = default);

    void SetClientCookie(HttpContext http, string clientId);
    void ClearClientCookie(HttpContext http);
}

public sealed record LoginBrandingContext(
    LoginTheme Theme,
    string? OAuthClientId,
    bool IsOAuthFlow);
