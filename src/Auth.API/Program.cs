using Auth.API.Data;
using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("MercantecSpa", policy =>
    {
        if (spaOrigins.Length > 0)
            policy.WithOrigins(spaOrigins).AllowAnyHeader().AllowAnyMethod();
        else
            policy.SetIsOriginAllowed(_ => false).AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

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
