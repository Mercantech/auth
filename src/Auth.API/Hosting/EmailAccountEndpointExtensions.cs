using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Hosting;

public static class EmailAccountEndpointExtensions
{
    public static WebApplication MapEmailAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/account/confirm-email", ConfirmEmailAsync).AllowAnonymous();
        app.MapPost("/account/email/resend", ResendConfirmationAsync).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/account/password/forgot", ForgotPasswordAsync).AllowAnonymous().DisableAntiforgery();
        app.MapPost("/account/password/reset", ResetPasswordAsync).AllowAnonymous().DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> ConfirmEmailAsync(
        HttpContext ctx,
        string? token,
        IUserActionTokenService tokenService,
        AuthDbContext db)
    {
        var user = await tokenService.ValidateAndConsumeAsync(
            token ?? string.Empty,
            UserActionTokenPurpose.EmailConfirmation,
            ctx.RequestAborted);

        if (user is null)
            return Results.Redirect(LoginBrandingUrls.Login(error: "confirm_invalid"));

        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.EmailConfirmed, true), ctx.RequestAborted);

        return Results.Redirect(LoginBrandingUrls.Login(error: "confirm_ok"));
    }

    private static async Task<IResult> ResendConfirmationAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        AuthDbContext db,
        ILocalAccountService localAccounts,
        IAccountEmailService accountEmail,
        EmailRateLimiter rateLimiter)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect(LoginBrandingUrls.ConfirmEmailSent(status: "invalid"));
        }

        var form = await ctx.Request.ReadFormAsync();
        var email = form["email"].ToString();
        var returnUrl = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var norm = EmailNormalizer.Normalize(email) ?? email.Trim().ToLowerInvariant();

        if (!rateLimiter.TryAcquire($"resend:ip:{ip}") || !rateLimiter.TryAcquire($"resend:email:{norm}"))
            return Results.Redirect(LoginBrandingUrls.ConfirmEmailSent(returnUrl, email, "rate_limit"));

        var user = await localAccounts.FindUserForPasswordSignInAsync(email, ctx.RequestAborted);
        if (user is not null && user.LocalLogin is not null && !user.EmailConfirmed && !user.IsDisabled)
        {
            try
            {
                await accountEmail.SendEmailConfirmationAsync(
                    user,
                    ResolveEmailClientId(ctx, returnUrl),
                    ctx.RequestAborted);
            }
            catch (Exception ex)
            {
                ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("EmailAccount")
                    .LogError(ex, "Kunne ikke gensende bekræftelsesmail til {Email}", email);
            }
        }

        return Results.Redirect(LoginBrandingUrls.ConfirmEmailSent(returnUrl, email, "resent"));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        ILocalAccountService localAccounts,
        IAccountEmailService accountEmail,
        EmailRateLimiter rateLimiter)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect(LoginBrandingUrls.ForgotPassword(error: "invalid"));
        }

        var form = await ctx.Request.ReadFormAsync();
        var email = form["email"].ToString();
        var returnUrl = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var norm = EmailNormalizer.Normalize(email) ?? email.Trim().ToLowerInvariant();

        if (!rateLimiter.TryAcquire($"forgot:ip:{ip}") || !rateLimiter.TryAcquire($"forgot:email:{norm}"))
            return Results.Redirect(LoginBrandingUrls.ForgotPassword(returnUrl, "rate_limit"));

        var user = await localAccounts.FindUserForPasswordSignInAsync(email, ctx.RequestAborted);
        if (user is not null && user.LocalLogin is not null && !user.IsDisabled)
        {
            try
            {
                await accountEmail.SendPasswordResetAsync(
                    user,
                    ResolveEmailClientId(ctx, returnUrl),
                    ctx.RequestAborted);
            }
            catch (Exception ex)
            {
                ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("EmailAccount")
                    .LogError(ex, "Kunne ikke sende nulstillingsmail til {Email}", email);
            }
        }

        return Results.Redirect(LoginBrandingUrls.ForgotPassword(returnUrl, "sent"));
    }

    private static async Task<IResult> ResetPasswordAsync(
        HttpContext ctx,
        IAntiforgery antiforgery,
        IUserActionTokenService tokenService,
        ILocalAccountService localAccounts,
        IAccountEmailService accountEmail)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
        }
        catch
        {
            return Results.Redirect(LoginBrandingUrls.ResetPassword(error: "invalid"));
        }

        var form = await ctx.Request.ReadFormAsync();
        var token = form["token"].ToString();
        var password = form["password"].ToString();
        var confirm = form["confirmPassword"].ToString();

        if (!string.Equals(password, confirm, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(password)
            || password.Length < 8
            || password.Length > 100)
        {
            return Results.Redirect(LoginBrandingUrls.ResetPassword(token, "password_invalid"));
        }

        var user = await tokenService.ValidateAndConsumeAsync(
            token,
            UserActionTokenPurpose.PasswordReset,
            ctx.RequestAborted);

        if (user is null || user.LocalLogin is null || user.IsDisabled)
            return Results.Redirect(LoginBrandingUrls.ResetPassword(error: "token_invalid"));

        var email = user.LocalLogin.Email;
        var result = await localAccounts.SetPasswordAsync(user.Id, email, password, ctx.RequestAborted);
        if (result is not (SetPasswordResult.Created or SetPasswordResult.Updated))
            return Results.Redirect(LoginBrandingUrls.ResetPassword(token, "password_invalid"));

        try
        {
            await accountEmail.SendPasswordChangedNoticeAsync(
                user,
                LoginBrandingUrls.ClientIdFromContext(ctx),
                ctx.RequestAborted);
        }
        catch (Exception ex)
        {
            ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("EmailAccount")
                .LogError(ex, "Kunne ikke sende adgangskode-ændret mail til {UserId}", user.Id);
        }

        return Results.Redirect(LoginBrandingUrls.Login(error: "password_reset_ok"));
    }

    private static string? ResolveEmailClientId(HttpContext ctx, string? returnUrl)
    {
        var fromReturn = LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(returnUrl, ctx);
        return string.IsNullOrWhiteSpace(fromReturn) ? null : fromReturn;
    }
}
