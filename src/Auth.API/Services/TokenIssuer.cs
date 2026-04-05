using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Services;

public class TokenIssuer(
    AuthDbContext db,
    IJwtSigningService signing,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time,
    ExternalOAuthTokensProtector externalTokens,
    MicrosoftIdentityTokenRefresher microsoftRefresher) : ITokenIssuer
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<(string accessToken, string refreshTokenPlain, DateTime accessExpiresUtc)> IssueTokensAsync(
        User user,
        IEnumerable<string> roleNames,
        string? deviceInfo,
        string? authMethod,
        string? externalOAuthTokensCipher = null,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var accessExpires = now.AddMinutes(_jwt.AccessTokenExpiryMinutes);

        var method = authMethod ?? user.LastLoginMethod;
        if (string.IsNullOrWhiteSpace(method))
            method = MercantecAuthClaims.LoginMethodValues.Unknown;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(MercantecAuthClaims.LoginMethod, method),
        };
        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

        foreach (var r in roleNames.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim(ClaimTypes.Role, r));

        var creds = new SigningCredentials(signing.RsaKey, SecurityAlgorithms.RsaSha256);
        var jwt = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: creds);
        jwt.Header["kid"] = signing.KeyId;
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refreshPlain = SecureToken.CreateOpaqueToken(48);
        var refreshHash = SecureToken.HashOpaqueToken(refreshPlain);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            DeviceInfo = deviceInfo,
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenExpiryDays),
            IsRevoked = false,
            AuthMethod = method,
            ExternalOAuthTokensCipher = externalOAuthTokensCipher,
        });
        await db.SaveChangesAsync(cancellationToken);

        return (accessToken, refreshPlain, accessExpires);
    }

    public async Task<(string accessToken, string refreshTokenPlain, DateTime accessExpiresUtc, IssuedMicrosoftAccess? microsoftAccess)?> RefreshAsync(
        string refreshTokenPlain,
        string? deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var hash = SecureToken.HashOpaqueToken(refreshTokenPlain);
        var existing = await db.RefreshTokens
            .Include(x => x.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);

        if (existing is null || existing.IsRevoked || existing.ExpiresAt < time.GetUtcNow().UtcDateTime)
            return null;

        var user = existing.User;
        if (user.IsDisabled)
            return null;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        string? newCipher = existing.ExternalOAuthTokensCipher;
        IssuedMicrosoftAccess? msAccess = null;

        if (!string.IsNullOrEmpty(newCipher)
            && existing.AuthMethod != null
            && existing.AuthMethod.StartsWith("microsoft", StringComparison.OrdinalIgnoreCase))
        {
            var payload = externalTokens.Unprotect(newCipher);
            if (payload is not null)
            {
                var refreshed = await microsoftRefresher.RefreshIfNeededAsync(payload, cancellationToken);
                if (refreshed is null)
                    return null;

                newCipher = externalTokens.Protect(refreshed);
                msAccess = ToIssuedMicrosoftAccess(refreshed);
            }
        }

        existing.IsRevoked = true;
        await db.SaveChangesAsync(cancellationToken);

        var (access, newRefresh, exp) = await IssueTokensAsync(user, roles, deviceInfo, existing.AuthMethod, newCipher, cancellationToken);
        return (access, newRefresh, exp, msAccess);
    }

    private static IssuedMicrosoftAccess ToIssuedMicrosoftAccess(ExternalOAuthTokensPayload p)
    {
        var expIn = p.AccessTokenExpiresAtUtc is { } exp
            ? Math.Max(0, (int)(exp - DateTimeOffset.UtcNow).TotalSeconds)
            : 3600;
        return new IssuedMicrosoftAccess(p.AccessToken, expIn);
    }
}
