using Auth.API.Models.Entities;

namespace Auth.API.Services;

/// <summary>Azure AD access token til Graph, returneres ved token/refresh når tilgængelig.</summary>
public sealed record IssuedMicrosoftAccess(string AccessToken, int ExpiresIn);

public interface ITokenIssuer
{
    Task<(string accessToken, string refreshTokenPlain, DateTime accessExpiresUtc)> IssueTokensAsync(
        User user,
        IEnumerable<string> roleNames,
        string? deviceInfo,
        string? authMethod,
        string? externalOAuthTokensCipher = null,
        CancellationToken cancellationToken = default);

    Task<(string accessToken, string refreshTokenPlain, DateTime accessExpiresUtc, IssuedMicrosoftAccess? microsoftAccess)?> RefreshAsync(
        string refreshTokenPlain,
        string? deviceInfo,
        CancellationToken cancellationToken = default);
}
