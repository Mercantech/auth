using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Auth.Tests.Unit;

public class ClientAppAdminServiceTests
{
    [Fact]
    public async Task AddRedirectUri_inserts_new_row_when_client_already_has_uris()
    {
        await using var db = CreateDb();
        var app = new ClientApp
        {
            Id = Guid.NewGuid(),
            ClientId = "sqlh1-test",
            Name = "SQL H1 Test",
            IsActive = true,
            IsPublic = true,
            RequirePkce = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.ClientApps.Add(app);
        db.ClientAppRedirectUris.Add(new ClientAppRedirectUri
        {
            Id = Guid.NewGuid(),
            ClientAppId = app.Id,
            Uri = "http://localhost:5173/auth/callback",
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var svc = new ClientAppAdminService(db, new StubHostEnvironment());
        var result = await svc.AddRedirectUriAsync(app.ClientId, "http://localhost:3000/auth/callback");

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, result.Client!.RedirectUris.Count);
        Assert.Contains(result.Client.RedirectUris, r => r.Uri == "http://localhost:3000/auth/callback");

        var rows = await db.ClientAppRedirectUris.AsNoTracking()
            .Where(r => r.ClientAppId == app.Id)
            .Select(r => r.Uri)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains("http://localhost:3000/auth/callback", rows);
    }

    private static AuthDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AuthDbContext(options);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Auth.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
