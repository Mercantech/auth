using System.Net;
using Auth.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class LoginBrandingTests(AuthIntegrationFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Login_with_oauth_returnUrl_and_mercanlink_theme_renders_theme_markup()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var app = await db.ClientApps.FirstOrDefaultAsync(c => c.ClientId == "demo");
        if (app is not null)
        {
            app.LoginThemeId = "mercanlink";
            await db.SaveChangesAsync();
        }

        var returnUrl = Uri.EscapeDataString("/oauth/authorize?client_id=demo&redirect_uri=http://localhost:5155/oauth/callback&response_type=code");
        var res = await _client.GetAsync($"/Account/Login?ReturnUrl={returnUrl}&client_id=demo");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("data-login-theme=\"mercanlink\"", html, StringComparison.Ordinal);
        Assert.Contains("/themes/mercanlink.css", html, StringComparison.Ordinal);
        Assert.Contains("Mercanlink", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_with_oauth_returnUrl_and_gf2learn_theme_renders_theme_markup()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var app = await db.ClientApps.FirstOrDefaultAsync(c => c.ClientId == "demo");
        if (app is not null)
        {
            app.LoginThemeId = "gf2learn";
            await db.SaveChangesAsync();
        }

        var returnUrl = Uri.EscapeDataString("/oauth/authorize?client_id=demo&redirect_uri=http://localhost:5155/oauth/callback&response_type=code");
        var res = await _client.GetAsync($"/Account/Login?ReturnUrl={returnUrl}&client_id=demo");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("data-login-theme=\"gf2learn\"", html, StringComparison.Ordinal);
        Assert.Contains("/themes/gf2learn.css", html, StringComparison.Ordinal);
        Assert.Contains("GF2 Learn", html, StringComparison.Ordinal);
        Assert.Contains("Grundforløb 2 programmering", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_with_oauth_returnUrl_and_uptimedaddy_theme_renders_theme_markup()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var app = await db.ClientApps.FirstOrDefaultAsync(c => c.ClientId == "demo");
        if (app is not null)
        {
            app.LoginThemeId = "uptimedaddy";
            await db.SaveChangesAsync();
        }

        var returnUrl = Uri.EscapeDataString("/oauth/authorize?client_id=demo&redirect_uri=http://localhost:5155/oauth/callback&response_type=code");
        var res = await _client.GetAsync($"/Account/Login?ReturnUrl={returnUrl}&client_id=demo");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var html = await res.Content.ReadAsStringAsync();
        Assert.Contains("data-login-theme=\"uptimedaddy\"", html, StringComparison.Ordinal);
        Assert.Contains("/themes/uptimedaddy.css", html, StringComparison.Ordinal);
        Assert.Contains("Uptime Daddy", html, StringComparison.Ordinal);
    }
}
