using System.Globalization;
using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Auth.API.Services;
using Auth.API.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableCors("MercantecSpa")]
public class OAuthController(
    AuthDbContext db,
    ExternalOAuthTokensProtector externalOAuthTokens,
    ITokenIssuer tokenIssuer,
    IOidcTokenService oidcTokens,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time) : ControllerBase
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    [HttpGet("/oauth/authorize")]
    public async Task<IActionResult> Authorize(
        [FromQuery] string? response_type,
        [FromQuery] string? client_id,
        [FromQuery] string? redirect_uri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? nonce,
        [FromQuery] string? code_challenge,
        [FromQuery] string? code_challenge_method,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(client_id) || string.IsNullOrEmpty(redirect_uri))
            return BadRequest("invalid_request");

        var client = await db.ClientApps
            .Include(c => c.RedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == client_id && c.IsActive, cancellationToken);
        if (client is null)
            return BadRequest("unauthorized_client");

        if (!client.RedirectUris.Any(r => string.Equals(r.Uri, redirect_uri, StringComparison.Ordinal)))
            return BadRequest("invalid_redirect_uri");

        if (!string.Equals(response_type, "code", StringComparison.Ordinal))
            return RedirectWithOAuthError(redirect_uri, "unsupported_response_type", null, state);

        if (client.RequirePkce)
        {
            if (string.IsNullOrEmpty(code_challenge) || !string.Equals(code_challenge_method, "S256", StringComparison.Ordinal))
                return RedirectWithOAuthError(redirect_uri, "invalid_request", "PKCE S256 required", state);
        }

        if (HttpContext.User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.Path + Request.QueryString;
            return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var appUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDisabled, cancellationToken);
        if (appUser is null)
            return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        var plainCode = SecureToken.CreateOpaqueToken(32);
        var now = time.GetUtcNow().UtcDateTime;
        var loginMethod = HttpContext.User.FindFirstValue(MercantecAuthClaims.LoginMethod)
            ?? appUser.LastLoginMethod
            ?? MercantecAuthClaims.LoginMethodValues.Unknown;

        var externalCipher = await TryBuildMicrosoftTokensCipherAsync(loginMethod, cancellationToken);

        db.AuthorizationCodes.Add(new AuthorizationCode
        {
            Id = Guid.NewGuid(),
            CodeHash = SecureToken.HashOpaqueToken(plainCode),
            UserId = appUser.Id,
            ClientStringId = client.ClientId,
            RedirectUri = redirect_uri,
            Scope = scope,
            Nonce = nonce,
            CodeChallenge = code_challenge,
            CodeChallengeMethod = code_challenge_method,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5),
            IsUsed = false,
            LoginMethod = loginMethod,
            ExternalOAuthTokensCipher = externalCipher,
        });
        await db.SaveChangesAsync(cancellationToken);

        var query = new Dictionary<string, string?> { ["code"] = plainCode };
        if (!string.IsNullOrEmpty(state))
            query["state"] = state;
        return Redirect(QueryHelpers.AddQueryString(redirect_uri, query));
    }

    [HttpPost("/oauth/token")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task<IActionResult> Token(CancellationToken cancellationToken = default)
    {
        var form = await Request.ReadFormAsync(cancellationToken);
        var grantType = form["grant_type"].ToString();

        if (grantType == "authorization_code")
            return await HandleAuthorizationCodeAsync(form, cancellationToken);
        if (grantType == "refresh_token")
            return await HandleRefreshTokenAsync(form, cancellationToken);

        return BadRequest(new { error = "unsupported_grant_type" });
    }

    private async Task<IActionResult> HandleAuthorizationCodeAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var clientId = form["client_id"].ToString();
        var clientSecret = form["client_secret"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(clientId))
            return BadRequest(new { error = "invalid_request" });

        var client = await db.ClientApps
            .Include(c => c.RedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);
        if (client is null)
            return Unauthorized(new { error = "invalid_client" });

        if (!client.IsPublic)
        {
            if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(client.ClientSecretHash)
                || !BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecretHash))
                return Unauthorized(new { error = "invalid_client" });
        }

        if (!client.RedirectUris.Any(r => string.Equals(r.Uri, redirectUri, StringComparison.Ordinal)))
            return BadRequest(new { error = "invalid_grant" });

        var hash = SecureToken.HashOpaqueToken(code);
        var authCode = await db.AuthorizationCodes
            .Include(x => x.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.CodeHash == hash, cancellationToken);

        if (authCode is null || authCode.IsUsed || authCode.ExpiresAt < time.GetUtcNow().UtcDateTime
            || !string.Equals(authCode.ClientStringId, clientId, StringComparison.Ordinal)
            || !string.Equals(authCode.RedirectUri, redirectUri, StringComparison.Ordinal))
            return BadRequest(new { error = "invalid_grant" });

        if (client.RequirePkce)
        {
            if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(authCode.CodeChallenge)
                || !string.Equals(authCode.CodeChallengeMethod, "S256", StringComparison.Ordinal)
                || !PkceHelper.VerifyS256(codeVerifier, authCode.CodeChallenge))
                return BadRequest(new { error = "invalid_grant" });
        }

        if (authCode.User.IsDisabled)
            return BadRequest(new { error = "invalid_grant" });

        authCode.IsUsed = true;
        var roles = authCode.User.UserRoles.Select(ur => ur.Role.Name).ToList();
        var device = Request.Headers.UserAgent.ToString();
        var authMethod = authCode.LoginMethod ?? authCode.User.LastLoginMethod;
        var (access, refresh, exp) = await tokenIssuer.IssueTokensAsync(
            authCode.User,
            roles,
            device,
            authMethod,
            authCode.ExternalOAuthTokensCipher,
            cancellationToken);

        var body = new Dictionary<string, object?>
        {
            ["access_token"] = access,
            ["refresh_token"] = refresh,
            ["token_type"] = "Bearer",
            ["expires_in"] = (int)(exp - time.GetUtcNow().UtcDateTime).TotalSeconds,
        };

        var scopes = SplitScopes(authCode.Scope);
        if (scopes.Contains("openid"))
        {
            var now = time.GetUtcNow().UtcDateTime;
            var idExp = now.AddMinutes(Math.Min(_jwt.AccessTokenExpiryMinutes, 30));
            body["id_token"] = oidcTokens.CreateIdToken(
                authCode.User,
                clientId,
                authCode.Nonce,
                scopes,
                authMethod,
                nowUtc: now,
                expiresUtc: idExp);
        }
        var msPayload = externalOAuthTokens.Unprotect(authCode.ExternalOAuthTokensCipher);
        if (msPayload is not null)
        {
            body["microsoft_access_token"] = msPayload.AccessToken;
            body["microsoft_expires_in"] = msPayload.AccessTokenExpiresAtUtc is { } e
                ? Math.Max(0, (int)(e - DateTimeOffset.UtcNow).TotalSeconds)
                : 3600;
        }

        return new JsonResult(body);
    }

    private async Task<IActionResult> HandleRefreshTokenAsync(IFormCollection form, CancellationToken cancellationToken)
    {
        var refresh = form["refresh_token"].ToString();
        var clientId = form["client_id"].ToString();
        if (string.IsNullOrEmpty(refresh) || string.IsNullOrEmpty(clientId))
            return BadRequest(new { error = "invalid_request" });

        var client = await db.ClientApps.FirstOrDefaultAsync(c => c.ClientId == clientId && c.IsActive, cancellationToken);
        if (client is null)
            return Unauthorized(new { error = "invalid_client" });

        if (!client.IsPublic)
        {
            var clientSecret = form["client_secret"].ToString();
            if (string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(client.ClientSecretHash)
                || !BCrypt.Net.BCrypt.Verify(clientSecret, client.ClientSecretHash))
                return Unauthorized(new { error = "invalid_client" });
        }

        var device = Request.Headers.UserAgent.ToString();
        var result = await tokenIssuer.RefreshAsync(refresh, device, cancellationToken);
        if (result is null)
            return BadRequest(new { error = "invalid_grant" });

        var (access, newRefresh, exp, msAccess) = result.Value;
        var body = new Dictionary<string, object?>
        {
            ["access_token"] = access,
            ["refresh_token"] = newRefresh,
            ["token_type"] = "Bearer",
            ["expires_in"] = (int)(exp - time.GetUtcNow().UtcDateTime).TotalSeconds,
        };
        if (msAccess is not null)
        {
            body["microsoft_access_token"] = msAccess.AccessToken;
            body["microsoft_expires_in"] = msAccess.ExpiresIn;
        }

        return new JsonResult(body);
    }

    private async Task<string?> TryBuildMicrosoftTokensCipherAsync(string loginMethod, CancellationToken cancellationToken)
    {
        if (!loginMethod.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase))
            return null;

        var authn = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authn.Succeeded || authn.Properties is null)
            return null;

        var access = authn.Properties.GetTokenValue("access_token");
        if (string.IsNullOrEmpty(access))
            return null;

        var payload = new ExternalOAuthTokensPayload
        {
            AccessToken = access,
            RefreshToken = authn.Properties.GetTokenValue("refresh_token"),
            AccessTokenExpiresAtUtc = ParseOAuthExpiresAt(authn.Properties.GetTokenValue("expires_at")),
        };
        return externalOAuthTokens.Protect(payload);
    }

    private static DateTimeOffset? ParseOAuthExpiresAt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto
            : null;
    }

    private static IActionResult RedirectWithOAuthError(string redirectUri, string error, string? description, string? state)
    {
        var query = new Dictionary<string, string?> { ["error"] = error };
        if (!string.IsNullOrEmpty(description))
            query["error_description"] = description;
        if (!string.IsNullOrEmpty(state))
            query["state"] = state;
        return new RedirectResult(QueryHelpers.AddQueryString(redirectUri, query));
    }

    private static HashSet<string> SplitScopes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.Ordinal);
        return raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }
}
