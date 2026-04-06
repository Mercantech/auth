using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class UserInfoTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task Userinfo_returns_profile_for_valid_access_token()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var tokenIssuer = scope.ServiceProvider.GetRequiredService<Auth.API.Services.ITokenIssuer>();

        var userRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Name == "User");
        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Userinfo User",
            Email = "userinfo@example.test",
            CreatedAt = now,
            LastLoginAt = now,
            LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await db.SaveChangesAsync();

        var (access, _, _) = await tokenIssuer.IssueTokensAsync(
            user,
            ["User"],
            deviceInfo: "xunit",
            authMethod: MercantecAuthClaims.LoginMethodValues.Password);

        var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

        var res = await client.GetAsync("/userinfo");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(user.Id.ToString(), doc.RootElement.GetProperty("sub").GetString());
        Assert.Equal("Userinfo User", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("userinfo@example.test", doc.RootElement.GetProperty("email").GetString());
    }
}

