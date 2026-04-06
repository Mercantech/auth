using Auth.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class DatabaseSeedTests(AuthIntegrationFixture fixture)
{
    [Fact]
    public async Task Migrations_and_seed_create_roles_and_demo_client()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var roles = await db.Roles.AsNoTracking().Select(r => r.Name).OrderBy(x => x).ToListAsync();
        Assert.Equal(["Admin", "User"], roles);

        var demo = await db.ClientApps
            .AsNoTracking()
            .Include(c => c.RedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == "demo");
        Assert.NotNull(demo);
        Assert.True(demo.IsPublic);
        Assert.True(demo.RequirePkce);
        Assert.Contains(demo.RedirectUris, r => r.Uri.Contains("5155", StringComparison.Ordinal));
    }
}
