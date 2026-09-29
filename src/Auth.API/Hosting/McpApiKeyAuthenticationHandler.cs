using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Auth.API.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Auth.API.Hosting;

/// <summary>Autentificerer admin-API-kald via header <c>x-api-key</c> (Dokploy-stil).</summary>
public sealed class McpApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<McpOptions> mcpOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string HeaderName = "x-api-key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var provided = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(provided))
            return Task.FromResult(AuthenticateResult.NoResult());

        var expected = mcpOptions.CurrentValue.ApiKey;
        if (string.IsNullOrWhiteSpace(expected))
            return Task.FromResult(AuthenticateResult.Fail("MCP API-nøgle er ikke konfigureret på serveren."));

        if (!FixedTimeEqualsUtf8(provided.Trim(), expected.Trim()))
            return Task.FromResult(AuthenticateResult.Fail("Ugyldig API-nøgle."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString("D")),
            new Claim(ClaimTypes.Name, "mcp-api-key"),
            new Claim(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, MercantecAuthSchemes.McpApiKey);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, MercantecAuthSchemes.McpApiKey);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEqualsUtf8(string a, string b)
    {
        var aHash = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var bHash = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(aHash, bHash);
    }
}
