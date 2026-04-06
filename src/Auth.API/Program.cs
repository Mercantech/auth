using Auth.API.Data;
using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IJwtSigningService, JwtSigningService>();
builder.Services.AddSingleton<IOidcTokenService, OidcTokenService>();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddSingleton<IReturnUrlValidator, ReturnUrlValidator>();
builder.Services.AddSingleton<ExternalOAuthTokensProtector>();
builder.Services.AddHttpClient<MicrosoftIdentityTokenRefresher>();
builder.Services.AddScoped<ITokenIssuer, TokenIssuer>();
builder.Services.AddScoped<IExternalAccountService, ExternalAccountService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "mercantec_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/signout";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { })
    .AddMercantecExternalLogins(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddRazorPages();

var spaOrigins = builder.Configuration.GetSection("Cors:SpaOrigins").Get<string[]>() ?? [];
var allowedDomains = builder.Configuration.GetSection("Cors:AllowedDomains").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("MercantecSpa", policy =>
    {
        static string NormalizeDomain(string d) => d.Trim().Trim('.').ToLowerInvariant();

        static bool HostMatchesDomainOrSubdomain(string host, string domain)
        {
            host = host.Trim().Trim('.').ToLowerInvariant();
            domain = NormalizeDomain(domain);
            if (domain.Length == 0)
                return false;
            if (string.Equals(host, domain, StringComparison.OrdinalIgnoreCase))
                return true;
            return host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
        }

        bool IsAllowedOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var u) || u is null)
                return false;
            if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps)
                return false;

            // Eksplicit allowlist (origin-level)
            foreach (var entry in spaOrigins)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;
                if (Uri.TryCreate(entry.Trim(), UriKind.Absolute, out var allowed) && allowed is not null)
                {
                    var allowedOrigin = $"{allowed.Scheme}://{allowed.Authority}";
                    var reqOrigin = $"{u.Scheme}://{u.Authority}";
                    if (string.Equals(allowedOrigin, reqOrigin, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // Domæner + subdomæner (host-level)
            foreach (var d in allowedDomains)
            {
                if (string.IsNullOrWhiteSpace(d))
                    continue;
                if (HostMatchesDomainOrSubdomain(u.Host, d))
                    return true;
            }

            return false;
        }

        policy.SetIsOriginAllowed(IsAllowedOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Når app'en kører bag reverse proxy (TLS terminering), kommer request ofte ind som http internt.
// OAuth middleware bygger redirect_uri ud fra Request.Scheme/Host, så vi skal respektere X-Forwarded-Proto/Host.
if (!app.Environment.IsDevelopment())
{
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        // Accepter flere proxy-hop (CDN -> LB -> reverse proxy).
        ForwardLimit = null,
    };

    // Default er loopback-only; ryd så vi respekterer headers fra ekstern proxy.
    forwarded.KnownProxies.Clear();
    forwarded.KnownIPNetworks.Clear();

    app.UseForwardedHeaders(forwarded);
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var jwt = scope.ServiceProvider.GetRequiredService<IJwtSigningService>();
    await jwt.EnsureKeysExistAsync();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await DbSeeder.SeedAsync(
        db,
        scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BootstrapOptions>>(),
        scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>());
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

app.UseStaticFiles();
app.UseRouting();
app.UseCors("MercantecSpa");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// API og minimal endpoints før Blazor, ellers kan catch-all (MapRazorComponents) fange f.eks. /.well-known/jwks.json.
app.MapControllers();
app.MapAccountEndpoints();
app.MapRazorPages();
app.MapRazorComponents<Auth.API.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Gør typen synlig for <c>WebApplicationFactory&lt;Program&gt;</c> i integrationstests.</summary>
public partial class Program;
