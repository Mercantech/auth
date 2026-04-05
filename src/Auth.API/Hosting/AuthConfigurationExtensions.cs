using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Auth.API.Hosting;

public static class AuthConfigurationExtensions
{
    public static AuthenticationBuilder AddMercantecExternalLogins(this AuthenticationBuilder auth, IConfiguration configuration)
    {
        var cookie = CookieAuthenticationDefaults.AuthenticationScheme;

        var googleId = configuration["OAuth:Google:ClientId"];
        if (!string.IsNullOrWhiteSpace(googleId))
        {
            auth.AddGoogle(options =>
            {
                options.ClientId = googleId;
                options.ClientSecret = configuration["OAuth:Google:ClientSecret"] ?? "";
                options.SignInScheme = cookie;
                options.Events.OnTicketReceived = ctx => OAuthPrincipalReplacer.ReplaceWithAppPrincipalAsync(ctx, "google");
            });
        }

        var msId = configuration["OAuth:Microsoft:ClientId"];
        if (!string.IsNullOrWhiteSpace(msId))
        {
            auth.AddMicrosoftAccount(options =>
            {
                options.ClientId = msId;
                options.ClientSecret = configuration["OAuth:Microsoft:ClientSecret"] ?? "";
                options.SignInScheme = cookie;
                options.SaveTokens = true;
                var scopeLine = configuration["OAuth:Microsoft:Scope"]
                    ?? "offline_access openid profile email https://graph.microsoft.com/User.Read";
                foreach (var part in scopeLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    options.Scope.Add(part);
                var tenant = configuration["OAuth:Microsoft:TenantId"];
                if (!string.IsNullOrWhiteSpace(tenant)
                    && !string.Equals(tenant, "common", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tenant, "organizations", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tenant, "consumers", StringComparison.OrdinalIgnoreCase))
                {
                    options.AuthorizationEndpoint = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize";
                    options.TokenEndpoint = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";
                }
                options.Events.OnTicketReceived = ctx => OAuthPrincipalReplacer.ReplaceWithAppPrincipalAsync(ctx, "microsoft");
            });
        }

        var ghId = configuration["OAuth:GitHub:ClientId"];
        if (!string.IsNullOrWhiteSpace(ghId))
        {
            auth.AddOAuth("GitHub", "GitHub", options =>
            {
                options.ClientId = ghId;
                options.ClientSecret = configuration["OAuth:GitHub:ClientSecret"] ?? "";
                options.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
                options.TokenEndpoint = "https://github.com/login/oauth/access_token";
                options.CallbackPath = "/signin-github";
                options.SignInScheme = cookie;
                options.Scope.Add("read:user");
                options.Scope.Add("user:email");
                options.UserInformationEndpoint = "https://api.github.com/user";
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                options.Backchannel ??= new HttpClient(new HttpClientHandler(), disposeHandler: true);
                options.Backchannel.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "MercantecAuth/1.0 (ASP.NET Core OAuth; +https://auth.mercantec.tech)");
                options.Events.OnCreatingTicket = ctx => LoadOAuthUserProfileIfNeededAsync(ctx, "GitHub");
                options.Events.OnTicketReceived = ctx => OAuthPrincipalReplacer.ReplaceWithAppPrincipalAsync(ctx, "github");
            });
        }

        var dcId = configuration["OAuth:Discord:ClientId"];
        if (!string.IsNullOrWhiteSpace(dcId))
        {
            auth.AddOAuth("Discord", "Discord", options =>
            {
                options.ClientId = dcId;
                options.ClientSecret = configuration["OAuth:Discord:ClientSecret"] ?? "";
                options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
                options.TokenEndpoint = "https://discord.com/api/oauth2/token";
                options.CallbackPath = "/signin-discord";
                options.SignInScheme = cookie;
                options.Scope.Add("identify");
                options.Scope.Add("email");
                options.UserInformationEndpoint = "https://discord.com/api/v10/users/@me";
                // Backchannel er null under AddOAuth-konfiguration — opret HttpClient før User-Agent sættes.
                options.Backchannel ??= new HttpClient(new HttpClientHandler(), disposeHandler: true);
                options.Backchannel.DefaultRequestHeaders.TryAddWithoutValidation(
                    "User-Agent",
                    "MercantecAuth/1.0 (ASP.NET Core OAuth; +https://auth.mercantec.tech)");
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
                // ASP.NET Core 10 OAuthHandler kalder CreatingTicket med tomt user-JSON; hent profil og kør ClaimActions.
                options.Events.OnCreatingTicket = ctx => LoadOAuthUserProfileIfNeededAsync(ctx, "Discord");
                options.Events.OnTicketReceived = ctx => OAuthPrincipalReplacer.ReplaceWithAppPrincipalAsync(ctx, "discord");
            });
        }

        return auth;
    }

    private static async Task LoadOAuthUserProfileIfNeededAsync(OAuthCreatingTicketContext ctx, string providerLabel)
    {
        if (ctx.Identity is null || string.IsNullOrEmpty(ctx.AccessToken))
            return;

        var userJson = ctx.User;
        var hasId = userJson.ValueKind == JsonValueKind.Object
                    && userJson.TryGetProperty("id", out var existingId)
                    && existingId.ValueKind is JsonValueKind.String or JsonValueKind.Number;

        if (hasId)
        {
            ctx.RunClaimActions();
            EnsureNameIdentifierFromJsonId(ctx, userJson);
            return;
        }

        if (string.IsNullOrEmpty(ctx.Options.UserInformationEndpoint))
            return;

        using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        request.Headers.TryAddWithoutValidation(
            "User-Agent",
            "MercantecAuth/1.0 (ASP.NET Core OAuth; +https://auth.mercantec.tech)");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await ctx.Backchannel.SendAsync(request, ctx.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ctx.HttpContext.RequestAborted);
            ctx.Fail($"{providerLabel} brugerinfo fejlede ({(int)response.StatusCode}): {errBody}");
            return;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ctx.HttpContext.RequestAborted);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ctx.HttpContext.RequestAborted);
        ctx.RunClaimActions(doc.RootElement);
        EnsureNameIdentifierFromJsonId(ctx, doc.RootElement);
    }

    private static void EnsureNameIdentifierFromJsonId(OAuthCreatingTicketContext ctx, JsonElement userJson)
    {
        if (ctx.Identity is null || ctx.Identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            return;
        if (!userJson.TryGetProperty("id", out var idEl))
            return;
        var snowflake = idEl.ValueKind switch
        {
            JsonValueKind.String => idEl.GetString(),
            JsonValueKind.Number => idEl.GetRawText(),
            _ => null,
        };
        if (string.IsNullOrEmpty(snowflake))
            return;
        ctx.Identity.AddClaim(new Claim(
            ClaimTypes.NameIdentifier,
            snowflake,
            ClaimValueTypes.String,
            ctx.Options.ClaimsIssuer));
    }
}
