using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class TokenIssuerIntegrationTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task IssueTokensAsync_produces_valid_RS256_jwt_for_database_user()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var issuer = scope.ServiceProvider.GetRequiredService<ITokenIssuer>();
        var signing = scope.ServiceProvider.GetRequiredService<IJwtSigningService>();
        var jwtOpt = scope.ServiceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;

        var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Test User",
            Email = "token-test@example.test",
            EmailConfirmed = true,
            CreatedAt = now,
            LastLoginAt = now,
            LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await db.SaveChangesAsync();

        var (access, refresh, _) = await issuer.IssueTokensAsync(
            user,
            ["User"],
            deviceInfo: "xunit",
            authMethod: MercantecAuthClaims.LoginMethodValues.Password);

        Assert.NotEmpty(refresh);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            access,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signing.RsaKey,
                ValidIssuer = jwtOpt.Issuer,
                ValidAudience = jwtOpt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            },
            out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("Test User", principal.FindFirstValue(JwtRegisteredClaimNames.Name));
        Assert.Equal(
            MercantecAuthClaims.LoginMethodValues.Password,
            principal.FindFirstValue(MercantecAuthClaims.LoginMethod));
        Assert.Contains(principal.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public async Task Issue_and_refresh_use_client_specific_lifetimes_and_bind_token_to_client()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var issuer = scope.ServiceProvider.GetRequiredService<ITokenIssuer>();
        var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Client Lifetime User",
            Email = $"token-lifetime-{Guid.NewGuid():N}@example.test",
            EmailConfirmed = true,
            CreatedAt = now,
            LastLoginAt = now,
            LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await db.SaveChangesAsync();

        var clientId = $"lifetime-{Guid.NewGuid():N}";
        var issuedAt = DateTime.UtcNow;
        var (_, refresh, accessExpires) = await issuer.IssueTokensAsync(
            user,
            ["User"],
            deviceInfo: "xunit",
            authMethod: MercantecAuthClaims.LoginMethodValues.Password,
            clientId: clientId,
            accessTokenExpiryMinutes: 480,
            refreshTokenExpiryDays: 60);

        Assert.InRange(
            accessExpires,
            issuedAt.AddMinutes(480).AddSeconds(-2),
            DateTime.UtcNow.AddMinutes(480).AddSeconds(2));

        var refreshHash = SecureToken.HashOpaqueToken(refresh);
        var storedRefresh = await db.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.TokenHash == refreshHash);
        Assert.Equal(clientId, storedRefresh.ClientId);
        Assert.InRange(
            storedRefresh.ExpiresAt,
            issuedAt.AddDays(60).AddSeconds(-2),
            DateTime.UtcNow.AddDays(60).AddSeconds(2));

        var wrongClientResult = await issuer.RefreshAsync(
            refresh,
            deviceInfo: "xunit",
            clientId: "another-client",
            accessTokenExpiryMinutes: 120,
            refreshTokenExpiryDays: 10);
        Assert.Null(wrongClientResult);

        var refreshedAt = DateTime.UtcNow;
        var correctClientResult = await issuer.RefreshAsync(
            refresh,
            deviceInfo: "xunit",
            clientId: clientId,
            accessTokenExpiryMinutes: 120,
            refreshTokenExpiryDays: 10);
        Assert.NotNull(correctClientResult);
        Assert.InRange(
            correctClientResult.Value.accessExpiresUtc,
            refreshedAt.AddMinutes(120).AddSeconds(-2),
            DateTime.UtcNow.AddMinutes(120).AddSeconds(2));
    }
}
