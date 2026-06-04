using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public static class DbSeeder
{
    public static async Task SeedAsync(AuthDbContext db, IOptions<BootstrapOptions> bootstrap, IWebHostEnvironment env, CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (!await db.Roles.AnyAsync(ct))
        {
            db.Roles.AddRange(
                new Role { Id = Guid.NewGuid(), Name = "User" },
                new Role { Id = Guid.NewGuid(), Name = "Admin" });
            await db.SaveChangesAsync(ct);
        }

        var adminEmail = bootstrap.Value.AdminEmail;
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin", ct);
            var user = await db.Users.Include(u => u.LocalLogin).Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.LocalLogin != null && u.LocalLogin.Email == adminEmail, ct);
            if (user is not null && !user.UserRoles.Any(ur => ur.RoleId == adminRole.Id))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
                await db.SaveChangesAsync(ct);
            }
        }

        if ((env.IsDevelopment() || env.IsEnvironment("Testing")) && !await db.ClientApps.AnyAsync(c => c.ClientId == "demo", ct))
        {
            var app = new ClientApp
            {
                Id = Guid.NewGuid(),
                ClientId = "demo",
                Name = "Demo client (dev)",
                IsActive = true,
                IsPublic = true,
                RequirePkce = true,
                CreatedAt = DateTime.UtcNow,
            };
            db.ClientApps.Add(app);
            db.ClientAppRedirectUris.AddRange(
                new ClientAppRedirectUri { Id = Guid.NewGuid(), ClientApp = app, Uri = "http://localhost:5155/oauth/callback" },
                new ClientAppRedirectUri { Id = Guid.NewGuid(), ClientApp = app, Uri = "http://127.0.0.1:5155/oauth/callback" },
                new ClientAppRedirectUri { Id = Guid.NewGuid(), ClientApp = app, Uri = "http://localhost:8080/oauth/callback" },
                new ClientAppRedirectUri { Id = Guid.NewGuid(), ClientApp = app, Uri = "http://127.0.0.1:8080/oauth/callback" });
            await db.SaveChangesAsync(ct);
        }

        await EnsureDemoSpaTestRedirectsAsync(db, env, ct);
    }

    /// <summary>Tilføjer ekstra localhost-URI'er til demo-klient (fx statisk HTML-test på port 5173).</summary>
    private static async Task EnsureDemoSpaTestRedirectsAsync(AuthDbContext db, IWebHostEnvironment env, CancellationToken ct)
    {
        if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
            return;

        var demo = await db.ClientApps
            .Include(c => c.RedirectUris)
            .FirstOrDefaultAsync(c => c.ClientId == "demo", ct);
        if (demo is null)
            return;

        var extra = new[]
        {
            "http://localhost:5173/callback.html",
            "http://127.0.0.1:5173/callback.html",
        };
        var added = false;
        foreach (var u in extra)
        {
            if (demo.RedirectUris.Any(r => string.Equals(r.Uri, u, StringComparison.Ordinal)))
                continue;
            db.ClientAppRedirectUris.Add(new ClientAppRedirectUri
            {
                Id = Guid.NewGuid(),
                ClientAppId = demo.Id,
                Uri = u,
            });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync(ct);

        await EnsureMercanlinkThemeAsync(db, ct);
        await EnsureGf2LearnThemeAsync(db, ct);
    }

    /// <summary>Sætter login-tema og login-metoder på Mercanlink OAuth-klienter hvis de findes.</summary>
    private static async Task EnsureMercanlinkThemeAsync(AuthDbContext db, CancellationToken ct)
    {
        var mercanlinkClientIds = new[] { "mercanlink", "Mercanlink-app" };
        var clients = await db.ClientApps
            .Where(c => mercanlinkClientIds.Contains(c.ClientId))
            .ToListAsync(ct);

        var changed = false;
        foreach (var client in clients)
        {
            if (!string.Equals(client.LoginThemeId, "mercanlink", StringComparison.OrdinalIgnoreCase))
            {
                client.LoginThemeId = "mercanlink";
                changed = true;
            }

            var mercanlinkMethods = "passkey,password,google,microsoft,microsoft_edu";
            if (!string.Equals(client.AllowedLoginMethods, mercanlinkMethods, StringComparison.OrdinalIgnoreCase))
            {
                client.AllowedLoginMethods = mercanlinkMethods;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }

    /// <summary>Sætter login-tema og login-metoder på GF2 Learn OAuth-klienten hvis den findes.</summary>
    private static async Task EnsureGf2LearnThemeAsync(AuthDbContext db, CancellationToken ct)
    {
        var gf2ClientIds = new[] { "gf2-learn" };
        var clients = await db.ClientApps
            .Where(c => gf2ClientIds.Contains(c.ClientId))
            .ToListAsync(ct);

        var changed = false;
        foreach (var client in clients)
        {
            if (!string.Equals(client.LoginThemeId, "gf2learn", StringComparison.OrdinalIgnoreCase))
            {
                client.LoginThemeId = "gf2learn";
                changed = true;
            }

            var gf2Methods = "passkey,password,google,microsoft,microsoft_edu";
            if (!string.Equals(client.AllowedLoginMethods, gf2Methods, StringComparison.OrdinalIgnoreCase))
            {
                client.AllowedLoginMethods = gf2Methods;
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}
