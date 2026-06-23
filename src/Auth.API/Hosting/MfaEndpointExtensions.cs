using System.Security.Claims;
using System.Text.Json;
using Auth.API.Data;
using Auth.API.Options;
using Auth.API.Security;
using Auth.API.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Hosting;

public static class MfaEndpointExtensions
{
    public static WebApplication MapMfaEndpoints(this WebApplication app)
    {
        var mfa = app.MapGroup("/account/mfa").RequireAuthorization(MfaPolicies.MfaStep);
        mfa.MapPost("/totp/setup", TotpSetupAsync).DisableAntiforgery();
        mfa.MapPost("/totp/confirm", TotpConfirmAsync).DisableAntiforgery();
        mfa.MapPost("/verify", MfaVerifyAsync).DisableAntiforgery();
        mfa.MapPost("/totp/disable", TotpDisableAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();

        app.MapPost("/account/mfa/totp/disable/passkey/options", TotpDisablePasskeyOptionsAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();
        app.MapPost("/account/mfa/totp/disable/passkey/complete", TotpDisablePasskeyCompleteAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();

        var passkeys = app.MapGroup("/account/passkeys");
        passkeys.MapPost("/register/options", PasskeyRegisterOptionsAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();
        passkeys.MapPost("/register/complete", PasskeyRegisterCompleteAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();
        passkeys.MapPost("/assert/options", PasskeyAssertOptionsAsync)
            .RequireAuthorization(MfaPolicies.MfaStep)
            .DisableAntiforgery();
        passkeys.MapPost("/assert/complete", PasskeyAssertCompleteAsync)
            .RequireAuthorization(MfaPolicies.MfaStep)
            .DisableAntiforgery();
        passkeys.MapPost("/login/options", PasskeyLoginOptionsAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        passkeys.MapPost("/login/complete", PasskeyLoginCompleteAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        passkeys.MapPost("/{id:guid}/delete", PasskeyDeleteAsync)
            .RequireAuthorization(MfaPolicies.FullSession)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> TotpSetupAsync(
        HttpContext ctx,
        ITotpMfaService totp,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.Redirect("/Account/Security?error=invalid_token");

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        await totp.BeginSetupAsync(userId, ctx.RequestAborted);
        return Results.Redirect("/Account/Security");
    }

    private static async Task<IResult> TotpConfirmAsync(
        HttpContext ctx,
        AuthDbContext db,
        ITotpMfaService totp,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.Redirect("/Account/Security?error=invalid_token");

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var form = await ctx.Request.ReadFormAsync();
        var code = form["code"].ToString();
        var result = await totp.ConfirmSetupAsync(userId, code, ctx.RequestAborted);
        if (result != TotpConfirmResult.Success)
            return Results.Redirect("/Account/Security?error=totp_confirm");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
        await TrySendSecurityEmailAsync(
            ctx,
            user,
            email => email.SendTotpEnabledNoticeAsync(user, LoginBrandingUrls.ClientIdFromContext(ctx), ctx.RequestAborted));

        return Results.Redirect("/Account/Security?totp_enabled=1");
    }

    private static async Task<IResult> MfaVerifyAsync(
        HttpContext ctx,
        AuthDbContext db,
        ITotpMfaService totp,
        IPasskeyService passkeys,
        IMfaGateService mfaGate,
        IAuthUsageTracker usage,
        IReturnUrlValidator urls,
        IAntiforgery antiforgery,
        IOptions<MfaOptions> mfaOptions)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.Redirect(LoginBrandingUrls.Mfa("/", LoginBrandingUrls.ClientIdFromContext(ctx), "invalid_token"));

        if (!TryGetUserId(ctx, out var userId) || !SignInHelper.IsMfaPending(ctx.User))
            return Results.Redirect("/Account/Login");

        var form = await ctx.Request.ReadFormAsync();
        var returnUrl = string.IsNullOrWhiteSpace(form["returnUrl"].ToString()) ? "/" : form["returnUrl"].ToString();
        if (!urls.IsSafePostLoginReturnUrl(returnUrl, ctx.Request))
            returnUrl = "/";

        var code = form["code"].ToString();
        var verified = false;
        if (!string.IsNullOrWhiteSpace(code))
        {
            verified = await totp.VerifyCodeAsync(userId, code, ctx.RequestAborted);
            if (verified)
                await usage.RecordMfaTotpVerifyAsync(userId, ctx.RequestAborted);
        }

        const string mfaAmr = MercantecAuthClaims.AmrValues.Otp;

        if (!verified)
            return Results.Redirect(LoginBrandingUrls.Mfa(returnUrl, LoginBrandingUrls.ClientIdFromContext(ctx), "invalid"));

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ctx.RequestAborted);

        var roles = user.UserRoles.Select(ur => ur.Role!.Name).ToList();
        var loginMethod = ctx.User.FindFirstValue(MercantecAuthClaims.LoginMethod)
            ?? user.LastLoginMethod
            ?? MercantecAuthClaims.LoginMethodValues.Unknown;

        await SignInHelper.CompleteMfaAsync(ctx, user, roles, loginMethod, mfaAmr);

        return returnUrl.StartsWith("/", StringComparison.Ordinal)
            ? Results.LocalRedirect(returnUrl)
            : Results.Redirect(returnUrl);
    }

    private static async Task<IResult> TotpDisableAsync(
        HttpContext ctx,
        AuthDbContext db,
        ITotpMfaService totp,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.Redirect("/Account/Security?error=invalid_token");

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var form = await ctx.Request.ReadFormAsync();
        if (!await totp.DisableAsync(userId, form["code"].ToString(), ctx.RequestAborted))
            return Results.Redirect("/Account/Security?error=totp_disable");

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
        await TrySendSecurityEmailAsync(
            ctx,
            user,
            email => email.SendTotpDisabledNoticeAsync(user, LoginBrandingUrls.ClientIdFromContext(ctx), ctx.RequestAborted));

        return Results.Redirect("/Account/Security?totp_disabled=1");
    }

    private static async Task<IResult> TotpDisablePasskeyOptionsAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        ITotpMfaService totp,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        if (!await totp.IsEnabledAsync(userId, ctx.RequestAborted))
            return Results.BadRequest();

        if (!await db.UserPasskeyCredentials.AnyAsync(c => c.UserId == userId, ctx.RequestAborted))
            return Results.BadRequest();

        var options = await passkeys.GetAssertionOptionsForUserAsync(userId, ctx.RequestAborted);
        return Results.Json(options);
    }

    private static async Task<IResult> TotpDisablePasskeyCompleteAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        ITotpMfaService totp,
        IAuthUsageTracker usage,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?error=invalid_token");

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        if (!await totp.IsEnabledAsync(userId, ctx.RequestAborted))
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?totp_disabled=1");

        var (assertion, _, _) = await Fido2JsonHelper.ReadAssertionBodyAsync(ctx.Request);
        if (assertion is null)
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?error=totp_disable_passkey");

        var auth = await passkeys.CompleteAssertionAsync(assertion, ctx.RequestAborted);
        if (auth != PasskeyAuthResult.Success)
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?error=totp_disable_passkey");

        var credOwner = await db.UserPasskeyCredentials
            .AsNoTracking()
            .Where(c => c.CredentialId == assertion.RawId)
            .Select(c => c.UserId)
            .FirstOrDefaultAsync(ctx.RequestAborted);
        if (credOwner != userId)
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?error=totp_disable_passkey");

        if (!await totp.DisableAfterTrustedVerificationAsync(userId, ctx.RequestAborted))
            return PasskeyLoginJsonRedirect(ctx, "/Account/Security?error=totp_disable_passkey");

        await usage.RecordPasskeyAuthAsync(userId, ctx.RequestAborted);

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
        await TrySendSecurityEmailAsync(
            ctx,
            user,
            email => email.SendTotpDisabledNoticeAsync(user, LoginBrandingUrls.ClientIdFromContext(ctx), ctx.RequestAborted));

        return PasskeyLoginJsonRedirect(ctx, "/Account/Security?totp_disabled=1");
    }

    private static async Task<IResult> PasskeyRegisterOptionsAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
        var options = await passkeys.GetRegistrationOptionsAsync(
            userId,
            user.Email ?? user.Id.ToString(),
            user.DisplayName,
            ctx.RequestAborted);
        return Results.Json(options);
    }

    private static async Task<IResult> PasskeyRegisterCompleteAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var (attestation, friendlyName) = await Fido2JsonHelper.ReadRegistrationAsync(ctx.Request);
        if (attestation is null)
            return Results.BadRequest();

        var result = await passkeys.CompleteRegistrationAsync(
            userId,
            attestation,
            friendlyName,
            ctx.RequestAborted);

        if (result != PasskeyRegisterResult.Success)
            return Results.BadRequest();

        await ctx.RequestServices.GetRequiredService<IAuthUsageTracker>()
            .RecordPasskeyRegisterAsync(userId, ctx.RequestAborted);

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
        var passkeyName = string.IsNullOrWhiteSpace(friendlyName) ? "Passkey" : friendlyName.Trim();
        await TrySendSecurityEmailAsync(
            ctx,
            user,
            email => email.SendPasskeyAddedNoticeAsync(
                user,
                passkeyName,
                LoginBrandingUrls.ClientIdFromContext(ctx),
                ctx.RequestAborted));

        return Results.Ok(new { ok = true });
    }

    private static async Task<IResult> PasskeyAssertOptionsAsync(
        HttpContext ctx,
        IPasskeyService passkeys,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var options = await passkeys.GetAssertionOptionsForUserAsync(userId, ctx.RequestAborted);
        return Results.Json(options);
    }

    private static async Task<IResult> PasskeyAssertCompleteAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        IAuthUsageTracker usage,
        IReturnUrlValidator urls,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        if (!TryGetUserId(ctx, out var userId) || !SignInHelper.IsMfaPending(ctx.User))
            return Results.Unauthorized();

        var (assertion, returnUrlBody, _) = await Fido2JsonHelper.ReadAssertionBodyAsync(ctx.Request);
        if (assertion is null)
            return Results.BadRequest();

        var auth = await passkeys.CompleteAssertionAsync(assertion, ctx.RequestAborted);
        if (auth != PasskeyAuthResult.Success)
            return Results.BadRequest();

        var returnUrl = string.IsNullOrWhiteSpace(returnUrlBody) ? "/" : returnUrlBody!;
        if (!urls.IsSafePostLoginReturnUrl(returnUrl, ctx.Request))
            returnUrl = "/";

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ctx.RequestAborted);

        var roles = user.UserRoles.Select(ur => ur.Role!.Name).ToList();
        var loginMethod = ctx.User.FindFirstValue(MercantecAuthClaims.LoginMethod)
            ?? MercantecAuthClaims.LoginMethodValues.Passkey;

        await usage.RecordPasskeyAuthAsync(userId, ctx.RequestAborted);
        await SignInHelper.CompleteMfaAsync(ctx, user, roles, loginMethod, MercantecAuthClaims.AmrValues.WebAuthn);

        // JSON så fetch-klienten kan navigere (Location-header er ikke læsbar ved redirect: manual).
        return PasskeyLoginJsonRedirect(ctx, returnUrl);
    }

    private static async Task<IResult> PasskeyLoginOptionsAsync(
        IPasskeyService passkeys,
        IAntiforgery antiforgery,
        HttpContext ctx)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.BadRequest();

        var options = await passkeys.GetPasswordlessLoginOptionsAsync(ctx.RequestAborted);
        return Results.Json(options);
    }

    private static async Task<IResult> PasskeyLoginCompleteAsync(
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        IMfaGateService mfaGate,
        IAuthUsageTracker usage,
        IReturnUrlValidator urls,
        IOptions<MfaOptions> mfaOptions,
        IAntiforgery antiforgery)
    {
        var clientId = LoginBrandingUrls.ClientIdFromContext(ctx);

        if (!await ValidateAntiforgery(ctx, antiforgery))
            return PasskeyLoginJsonRedirect(ctx, LoginBrandingUrls.Login(null, "invalid", clientId));

        var (assertion, returnUrlBody, _) = await Fido2JsonHelper.ReadAssertionBodyAsync(ctx.Request);
        if (assertion is null)
            return PasskeyLoginJsonRedirect(ctx, LoginBrandingUrls.Login(returnUrlBody, "invalid", clientId));

        var returnUrlForPolicy = string.IsNullOrWhiteSpace(returnUrlBody) ? "/" : returnUrlBody!;
        if (!await ClientLoginMethodsHttpExtensions.IsLoginMethodAllowedAsync(
                ctx, ClientLoginMethodCatalog.Passkey.Id, returnUrlForPolicy, ctx.RequestAborted))
            return PasskeyLoginJsonRedirect(ctx, LoginBrandingUrls.Login(returnUrlBody, "provider", clientId));

        var auth = await passkeys.CompleteAssertionAsync(assertion, ctx.RequestAborted);
        if (auth != PasskeyAuthResult.Success)
            return PasskeyLoginJsonRedirect(ctx, LoginBrandingUrls.Login(returnUrlBody, "passkey", clientId));

        var credId = assertion.RawId;
        var stored = await db.UserPasskeyCredentials
            .AsNoTracking()
            .FirstAsync(c => c.CredentialId == credId, ctx.RequestAborted);

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == stored.UserId, ctx.RequestAborted);

        if (user.IsDisabled)
            return PasskeyLoginJsonRedirect(ctx, LoginBrandingUrls.Login(returnUrlBody, "disabled", clientId));

        var returnUrl = string.IsNullOrWhiteSpace(returnUrlBody) ? "/" : returnUrlBody!;
        if (!urls.IsSafePostLoginReturnUrl(returnUrl, ctx.Request))
            returnUrl = "/";

        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastLoginMethod, MercantecAuthClaims.LoginMethodValues.Passkey),
                ctx.RequestAborted);

        await usage.RecordPasskeyAuthAsync(user.Id, ctx.RequestAborted);

        var roles = user.UserRoles.Select(ur => ur.Role!.Name).ToList();
        var mfaUrl = await SignInHelper.EstablishSessionAfterPrimaryAuthAsync(
            ctx,
            user,
            roles,
            MercantecAuthClaims.LoginMethodValues.Passkey,
            returnUrl,
            mfaGate,
            mfaOptions,
            [MercantecAuthClaims.AmrValues.WebAuthn]);

        if (mfaUrl is not null)
            return PasskeyLoginJsonRedirect(ctx, mfaUrl);

        return PasskeyLoginJsonRedirect(ctx, returnUrl);
    }

    private static IResult PasskeyLoginJsonRedirect(HttpContext ctx, string path) =>
        Results.Json(new { redirect = ToAppRedirectPath(ctx, path) });

    private static string ToAppRedirectPath(HttpContext ctx, string path) =>
        path.StartsWith("/", StringComparison.Ordinal) && !string.IsNullOrEmpty(ctx.Request.PathBase)
            ? ctx.Request.PathBase + path
            : path;

    private static async Task<IResult> PasskeyDeleteAsync(
        Guid id,
        HttpContext ctx,
        AuthDbContext db,
        IPasskeyService passkeys,
        IAntiforgery antiforgery)
    {
        if (!await ValidateAntiforgery(ctx, antiforgery))
            return Results.Redirect("/Account/Security?error=invalid_token");

        if (!TryGetUserId(ctx, out var userId))
            return Results.Unauthorized();

        var removedName = await passkeys.DeleteAsync(userId, id, ctx.RequestAborted);
        if (removedName is not null)
        {
            var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, ctx.RequestAborted);
            await TrySendSecurityEmailAsync(
                ctx,
                user,
                email => email.SendPasskeyRemovedNoticeAsync(
                    user,
                    removedName,
                    LoginBrandingUrls.ClientIdFromContext(ctx),
                    ctx.RequestAborted));
        }

        return Results.Redirect("/Account/Security?passkey_removed=1");
    }

    private static bool TryGetUserId(HttpContext ctx, out Guid userId)
    {
        userId = default;
        return Guid.TryParse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static async Task TrySendSecurityEmailAsync(
        HttpContext ctx,
        Models.Entities.User user,
        Func<IAccountEmailService, Task> send)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            return;

        try
        {
            await send(ctx.RequestServices.GetRequiredService<IAccountEmailService>());
        }
        catch (Exception ex)
        {
            ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("SecurityEmail")
                .LogError(ex, "Kunne ikke sende sikkerhedsmail til {UserId}", user.Id);
        }
    }

    private static async Task<bool> ValidateAntiforgery(HttpContext ctx, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(ctx);
            return true;
        }
        catch
        {
            return false;
        }
    }

}
