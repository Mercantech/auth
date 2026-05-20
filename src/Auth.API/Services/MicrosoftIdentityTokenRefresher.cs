using System.Net.Http.Headers;
using System.Text.Json;
using Auth.API.Hosting;
using Auth.API.Models;
using Microsoft.Extensions.Logging;

namespace Auth.API.Services;

/// <summary>Fornyer Azure AD access token via refresh_token (Microsoft identity platform v2).</summary>
public sealed class MicrosoftIdentityTokenRefresher(
    HttpClient http,
    IConfiguration configuration,
    ILogger<MicrosoftIdentityTokenRefresher> log)
{
    public async Task<ExternalOAuthTokensPayload?> RefreshIfNeededAsync(
        ExternalOAuthTokensPayload payload,
        string? authMethod,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (payload.AccessTokenExpiresAtUtc is { } exp && exp > now.AddMinutes(2))
            return payload;

        if (string.IsNullOrEmpty(payload.RefreshToken))
        {
            if (payload.AccessTokenExpiresAtUtc is null || payload.AccessTokenExpiresAtUtc > now)
                return payload;
            return null;
        }

        var section = MicrosoftOAuthConfiguration.SectionForAuthMethod(authMethod);
        var clientId = configuration[$"{section}:ClientId"];
        var clientSecret = configuration[$"{section}:ClientSecret"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            log.LogWarning("Microsoft OAuth ({Section}) mangler — kan ikke forny Azure access token.", section);
            return payload.AccessTokenExpiresAtUtc is null || payload.AccessTokenExpiresAtUtc > now ? payload : null;
        }

        var tokenUrl = MicrosoftOAuthConfiguration.ResolveTokenEndpoint(configuration, section);
        var scope = MicrosoftOAuthConfiguration.ResolveScope(configuration, section);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = payload.RefreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = scope,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("Azure token refresh fejlede {Status} ({Section}): {Body}",
                (int)response.StatusCode, section, body.Length > 400 ? body[..400] : body);
            return payload.AccessTokenExpiresAtUtc is null || payload.AccessTokenExpiresAtUtc > now ? payload : null;
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var access = root.GetProperty("access_token").GetString();
        if (string.IsNullOrEmpty(access))
            return null;

        var newRefresh = root.TryGetProperty("refresh_token", out var rt) && rt.ValueKind == JsonValueKind.String
            ? rt.GetString()
            : payload.RefreshToken;

        var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var secs)
            ? secs
            : 3600;

        return new ExternalOAuthTokensPayload
        {
            AccessToken = access,
            RefreshToken = newRefresh,
            AccessTokenExpiresAtUtc = now.AddSeconds(expiresIn),
        };
    }
}
