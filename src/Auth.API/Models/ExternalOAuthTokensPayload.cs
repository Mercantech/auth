namespace Auth.API.Models;

/// <summary>Azure AD (Microsoft identity) tokens gemt krypteret sammen med Mercantec refresh.</summary>
public sealed class ExternalOAuthTokensPayload
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }

    /// <summary>UTC udløb for Azure access token.</summary>
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; set; }
}
