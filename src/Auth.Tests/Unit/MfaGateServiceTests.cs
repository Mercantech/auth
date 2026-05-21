using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Tests.Unit;

public class MfaGateServiceTests
{
    [Fact]
    public async Task RequiresMfaStepAsync_returns_false_when_primary_login_is_passkey()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserPasskeyCredentials.Add(new UserPasskeyCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CredentialId = [1, 2, 3],
            PublicKey = [4],
            FriendlyName = "Test",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var gate = new MfaGateService(db, Options.Create(new MfaOptions()));
        var requires = await gate.RequiresMfaStepAsync(
            userId,
            [],
            MercantecAuthClaims.LoginMethodValues.Passkey);

        Assert.False(requires);
    }

    [Fact]
    public async Task RequiresMfaStepAsync_returns_true_when_password_login_and_passkey_configured()
    {
        await using var db = CreateDb();
        var userId = Guid.NewGuid();
        db.UserPasskeyCredentials.Add(new UserPasskeyCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CredentialId = [1, 2, 3],
            PublicKey = [4],
            FriendlyName = "Test",
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var gate = new MfaGateService(db, Options.Create(new MfaOptions()));
        var requires = await gate.RequiresMfaStepAsync(
            userId,
            [],
            MercantecAuthClaims.LoginMethodValues.Password);

        Assert.True(requires);
    }

    private static AuthDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }
}
