using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class OAuthTokenExchangeTests(AuthIntegrationFixture fixture)
{
    private static string CreateS256Challenge(string codeVerifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        return PkceHelper.Base64UrlEncode(hash);
    }

    [Fact]
    public async Task Token_authorization_code_grant_returns_bearer_tokens_with_valid_pkce()
    {
        const string redirectUri = "http://localhost:5155/oauth/callback";
        const string clientId = "demo";
        const string codeVerifier = "0123456789012345678901234567890123456789012";
        const string nonce = "nonce-123";

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = "OAuth Code User",
                Email = "oauth-code@example.test",
                CreatedAt = now,
                LastLoginAt = now,
                LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });

            var plainCode = SecureToken.CreateOpaqueToken(24);
            db.AuthorizationCodes.Add(new AuthorizationCode
            {
                Id = Guid.NewGuid(),
                CodeHash = SecureToken.HashOpaqueToken(plainCode),
                UserId = user.Id,
                ClientStringId = clientId,
                RedirectUri = redirectUri,
                Scope = "openid profile email",
                Nonce = nonce,
                CodeChallenge = CreateS256Challenge(codeVerifier),
                CodeChallengeMethod = "S256",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5),
                IsUsed = false,
                LoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            });
            await db.SaveChangesAsync();

            var client = fixture.Factory.CreateClient();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = plainCode,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = codeVerifier,
            });

            var res = await client.PostAsync("/oauth/token", form);

            Assert.True(res.IsSuccessStatusCode, await res.Content.ReadAsStringAsync());
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            Assert.Equal("Bearer", root.GetProperty("token_type").GetString());
            Assert.True(root.GetProperty("access_token").GetString()?.Length > 20);
            Assert.True(root.GetProperty("refresh_token").GetString()?.Length > 20);
            Assert.True(root.GetProperty("expires_in").GetInt32() > 0);
            Assert.True(root.GetProperty("id_token").GetString()?.Length > 20);
        }
    }

    [Fact]
    public async Task Token_authorization_code_grant_rejects_wrong_pkce_verifier()
    {
        const string redirectUri = "http://localhost:5155/oauth/callback";
        const string clientId = "demo";
        const string codeVerifier = "0123456789012345678901234567890123456789012";

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = "OAuth PKCE Fail",
                CreatedAt = now,
                LastLoginAt = now,
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });

            var plainCode = SecureToken.CreateOpaqueToken(24);
            db.AuthorizationCodes.Add(new AuthorizationCode
            {
                Id = Guid.NewGuid(),
                CodeHash = SecureToken.HashOpaqueToken(plainCode),
                UserId = user.Id,
                ClientStringId = clientId,
                RedirectUri = redirectUri,
                CodeChallenge = CreateS256Challenge(codeVerifier),
                CodeChallengeMethod = "S256",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5),
                IsUsed = false,
            });
            await db.SaveChangesAsync();

            var client = fixture.Factory.CreateClient();
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = plainCode,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = "wrong-verifier-01234567890123456789012345678901",
            });

            var res = await client.PostAsync("/oauth/token", form);

            Assert.Equal(System.Net.HttpStatusCode.BadRequest, res.StatusCode);
        }
    }

    [Fact]
    public async Task Token_authorization_code_grant_uses_client_specific_lifetimes()
    {
        const string redirectUri = "http://localhost:5155/oauth/client-lifetime-callback";
        const string codeVerifier = "0123456789012345678901234567890123456789012";
        var clientId = $"lifetime-{Guid.NewGuid():N}";
        string plainCode;

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
            var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = "OAuth Lifetime User",
                Email = $"oauth-lifetime-{Guid.NewGuid():N}@example.test",
                CreatedAt = now,
                LastLoginAt = now,
                LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            };
            var clientApp = new ClientApp
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                Name = "Lifetime test client",
                IsActive = true,
                IsPublic = true,
                RequirePkce = true,
                AccessTokenExpiryMinutes = 120,
                RefreshTokenExpiryDays = 45,
                CreatedAt = now,
            };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
            db.ClientApps.Add(clientApp);
            db.ClientAppRedirectUris.Add(new ClientAppRedirectUri
            {
                Id = Guid.NewGuid(),
                ClientApp = clientApp,
                Uri = redirectUri,
            });

            plainCode = SecureToken.CreateOpaqueToken(24);
            db.AuthorizationCodes.Add(new AuthorizationCode
            {
                Id = Guid.NewGuid(),
                CodeHash = SecureToken.HashOpaqueToken(plainCode),
                UserId = user.Id,
                ClientStringId = clientId,
                RedirectUri = redirectUri,
                Scope = "openid profile email",
                CodeChallenge = CreateS256Challenge(codeVerifier),
                CodeChallengeMethod = "S256",
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(5),
                IsUsed = false,
                LoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            });
            await db.SaveChangesAsync();
        }

        var httpClient = fixture.Factory.CreateClient();
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = plainCode,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier,
        });

        var issuedAt = DateTime.UtcNow;
        var response = await httpClient.PostAsync("/oauth/token", form);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.InRange(root.GetProperty("expires_in").GetInt32(), 7_190, 7_200);

        var refresh = root.GetProperty("refresh_token").GetString();
        Assert.NotNull(refresh);
        var refreshHash = SecureToken.HashOpaqueToken(refresh);
        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var storedRefresh = await verifyDb.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.TokenHash == refreshHash);
        Assert.InRange(
            storedRefresh.ExpiresAt,
            issuedAt.AddDays(45).AddSeconds(-2),
            DateTime.UtcNow.AddDays(45).AddSeconds(2));
    }
}
