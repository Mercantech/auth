using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Auth.API.Hosting;

public static class AccountEndpointExtensions
{
    public static WebApplication MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/signin/challenge", ChallengeExternalLoginAsync).AllowAnonymous();

        app.MapGet("/account/link/start", StartAccountLinkAsync).RequireAuthorization();

        app.MapPost("/account/link/remove", RemoveAccountLinkAsync).RequireAuthorization();

        app.MapPost("/signin", HandleSignInAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        app.MapPost("/signup", HandleSignUpAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        app.MapGet("/signout", async (HttpContext ctx, IReturnUrlValidator urls, string? returnUrl) =>
            {
                await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (string.IsNullOrWhiteSpace(returnUrl))
                    return Results.LocalRedirect("/");
                if (!urls.IsSafePostLogoutRedirectUrl(returnUrl, ctx.Request))
                    return Results.LocalRedirect("/");
                if (returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal))
                    return Results.LocalRedirect(returnUrl);
                return Results.Redirect(returnUrl);
            })
            .AllowAnonymous();

        return app;
    }

    private static Task<IResult> ChallengeExternalLoginAsync(
        HttpContext ctx,
        string provider,
        string? returnUrl,
        string? emailKind,
        IReturnUrlValidator urls,
        IConfiguration config) =>
        OAuthChallengeCoreAsync(ctx, provider, returnUrl, emailKind, urls, config, attachAccountLinkTarget: false);

    private static Task<IResult> StartAccountLinkAsync(
        HttpContext ctx,
        string provider,
        string? returnUrl,
        string? emailKind,
        IReturnUrlValidator urls,
        IConfiguration config) =>
        OAuthChallengeCoreAsync(ctx, provider, returnUrl, emailKind, urls, config, attachAccountLinkTarget: true);

    /// <summary>Delt OAuth-challenge til login og eksplicit kontolinking.</summary>
    private static Task<IResult> OAuthChallengeCoreAsync(
        HttpContext ctx,
        string provider,
        string? returnUrl,
        string? emailKind,
        IReturnUrlValidator urls,
        IConfiguration config,
        bool attachAccountLinkTarget)
    {
        returnUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? (attachAccountLinkTarget ? "/Account/LinkedAccounts" : "/")
            : returnUrl!;
        if (!urls.IsSafePostLoginReturnUrl(returnUrl, ctx.Request))
            return Task.FromResult(Results.BadRequest("Ugyldig returnUrl."));

        var redirectUri = returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}{returnUrl}"
            : returnUrl;
        var props = new AuthenticationProperties { RedirectUri = redirectUri };

        if (attachAccountLinkTarget
            && Guid.TryParse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var me))
        {
            props.Items[AccountLinkAuthProperties.TargetUserIdKey] = me.ToString("D");
        }

        var providerKey = provider.Trim().ToLowerInvariant();
        var scheme = providerKey switch
        {
            "google" when !string.IsNullOrEmpty(config["OAuth:Google:ClientId"]) => GoogleDefaults.AuthenticationScheme,
            "microsoft" when MicrosoftOAuthConfiguration.IsConfigured(config, MicrosoftOAuthConfiguration.WorkSection) =>
                MercantecAuthSchemes.MicrosoftWork,
            "microsoft-edu" or "microsoftedu"
                when MicrosoftOAuthConfiguration.IsConfigured(config, MicrosoftOAuthConfiguration.EduSection) =>
                MercantecAuthSchemes.MicrosoftEdu,
            "github" when !string.IsNullOrEmpty(config["OAuth:GitHub:ClientId"]) => "GitHub",
            "discord" when !string.IsNullOrEmpty(config["OAuth:Discord:ClientId"]) => "Discord",
            _ => null,
        };

        if (scheme is null)
            return Task.FromResult(Results.BadRequest("Provider ikke konfigureret."));

        var emailKindForCookie = providerKey is "microsoft-edu" or "microsoftedu" && string.IsNullOrWhiteSpace(emailKind)
            ? "school"
            : emailKind;
        OAuthEmailKindCookie.Append(ctx, OAuthEmailKindCookie.ParseQuery(emailKindForCookie));
        return Task.FromResult(Results.Challenge(props, [scheme]));
    }

    private static async Task<IResult> RemoveAccountLinkAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        IExternalAccountService accounts,
        IReturnUrlValidator urls)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect("/Account/LinkedAccounts?error=invalid_token");
        }

        var form = await ctx.Request.ReadFormAsync();
        var ru = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/Account/LinkedAccounts" : form["returnUrl"].ToString();
        if (!urls.IsSafePostLoginReturnUrl(ru, ctx.Request))
            ru = "/Account/LinkedAccounts";

        if (!Guid.TryParse(form["id"].ToString(), out var externalLoginId)
            || !Guid.TryParse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            return RedirectWithQuery(ru, "error=invalid");

        var result = await accounts.UnlinkExternalLoginAsync(userId, externalLoginId, ctx.RequestAborted);
        var q = result switch
        {
            UnlinkExternalLoginResult.Success => "unlinked=1",
            UnlinkExternalLoginResult.NotFound => "error=not_found",
            UnlinkExternalLoginResult.CannotRemoveLastLoginMethod => "error=last_login",
            _ => "error=unknown",
        };

        return RedirectWithQuery(ru, q);
    }

    private static IResult RedirectWithQuery(string returnUrl, string queryPair)
    {
        var sep = returnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var target = $"{returnUrl}{sep}{queryPair}";
        return Results.Redirect(target, permanent: false);
    }

    private static async Task<IResult> HandleSignInAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        AuthDbContext db,
        IReturnUrlValidator returnUrlValidator)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect("/Account/Login?error=invalid");
        }

        var form = await ctx.Request.ReadFormAsync();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();

        if (!returnUrlValidator.IsSafePostLoginReturnUrl(returnUrl, ctx.Request))
            return Results.Redirect("/Account/Login?error=invalid");

        if (!ctx.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.EnableEmailPasswordLogin)
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&error=local_disabled");

        var user = await db.Users
            .Include(u => u.LocalLogin)
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.LocalLogin != null && u.LocalLogin.Email == email);

        if (user?.LocalLogin is null || !BCrypt.Net.BCrypt.Verify(password, user.LocalLogin.PasswordHash))
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&error=invalid");

        if (user.IsDisabled)
            return Results.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}&error=disabled");

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LastLoginMethod, MercantecAuthClaims.LoginMethodValues.Password));
        await SignInHelper.SignInAsync(ctx, user, roles);

        return returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? Results.LocalRedirect(returnUrl)
            : Results.Redirect(returnUrl);
    }

    private static async Task<IResult> HandleSignUpAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        AuthDbContext db,
        IReturnUrlValidator returnUrlValidator,
        IOptions<BootstrapOptions> bootstrap)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect("/Account/Register?error=invalid");
        }

        var form = await ctx.Request.ReadFormAsync();
        if (!ctx.RequestServices.GetRequiredService<IOptions<AuthOptions>>().Value.EnableEmailPasswordLogin)
        {
            var ru = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();
            return Results.Redirect($"/Account/Register?returnUrl={Uri.EscapeDataString(ru)}&error=local_disabled");
        }

        var displayName = form["displayName"].ToString();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();

        if (!returnUrlValidator.IsSafePostLoginReturnUrl(returnUrl, ctx.Request)
            || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 120
            || string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 100)
            return Results.Redirect($"/Account/Register?returnUrl={Uri.EscapeDataString(returnUrl)}&error=invalid");

        if (await db.LocalLogins.AnyAsync(l => l.Email == email))
            return Results.Redirect($"/Account/Register?returnUrl={Uri.EscapeDataString(returnUrl)}&error=email");

        var normSignup = EmailNormalizer.Normalize(email);
        if (normSignup is not null && await db.UserEmails.AnyAsync(e => e.NormalizedEmail == normSignup))
            return Results.Redirect($"/Account/Register?returnUrl={Uri.EscapeDataString(returnUrl)}&error=email");

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            Email = email,
            EmailConfirmed = false,
            CreatedAt = now,
            LastLoginAt = now,
            LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
        };
        db.Users.Add(user);
        db.LocalLogins.Add(new LocalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = now,
        });

        if (normSignup is not null)
        {
            db.UserEmails.Add(new UserEmail
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                NormalizedEmail = normSignup,
                Kind = UserEmailKind.Personal,
                LinkedAt = now,
            });
        }

        var userRole = await db.Roles.FirstAsync(r => r.Name == "User");
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });

        var adminEmail = bootstrap.Value.AdminEmail;
        if (!string.IsNullOrWhiteSpace(adminEmail)
            && string.Equals(email, adminEmail, StringComparison.OrdinalIgnoreCase))
        {
            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        }

        await db.SaveChangesAsync();

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();
        await SignInHelper.SignInAsync(ctx, user, roles);

        return returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? Results.LocalRedirect(returnUrl)
            : Results.Redirect(returnUrl);
    }
}
