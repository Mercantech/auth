using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public static class OAuthPrincipalReplacer
{
    public static async Task ReplaceWithAppPrincipalAsync(TicketReceivedContext ctx, string providerKey)
    {
        var sync = ctx.HttpContext.RequestServices.GetRequiredService<IExternalAccountService>();
        var db = ctx.HttpContext.RequestServices.GetRequiredService<AuthDbContext>();
        var usage = ctx.HttpContext.RequestServices.GetRequiredService<IAuthUsageTracker>();
        var mfaGate = ctx.HttpContext.RequestServices.GetRequiredService<IMfaGateService>();
        var mfaOptions = ctx.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<MfaOptions>>();
        var principal = ctx.Principal ?? throw new InvalidOperationException("Mangler principal efter OAuth.");
        var emailKind = OAuthEmailKindCookie.ReadAndClear(ctx.HttpContext);
        var isLinkMode = TryGetAccountLinkTargetUserId(ctx.Properties, out var linkTargetId);

        Guid userId;
        if (isLinkMode)
        {
            var outcome = await sync.LinkExternalToUserAsync(
                linkTargetId,
                principal,
                providerKey,
                emailKind,
                ctx.HttpContext.RequestAborted);
            if (outcome == LinkExternalOutcome.ConflictOtherUser)
            {
                ctx.HandleResponse();
                var pathBase = ctx.HttpContext.Request.PathBase.Value?.TrimEnd('/') ?? "";
                ctx.HttpContext.Response.Redirect(
                    $"{pathBase}/Account/LinkedAccounts?error=link_conflict");
                return;
            }

            userId = linkTargetId;
        }
        else
        {
            userId = await sync.FindOrLinkUserAsync(
                principal,
                providerKey,
                emailKind,
                ctx.HttpContext.RequestAborted);
        }

        var userEntity = await db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ctx.HttpContext.RequestAborted);

        var loginMethod = MercantecAuthClaims.ForOAuth(providerKey, emailKind);

        if (isLinkMode)
            await usage.RecordAccountLinkAsync(userId, providerKey, loginMethod, ctx.HttpContext.RequestAborted);
        else
            await usage.RecordProviderLoginAsync(userId, providerKey, loginMethod, ctx.HttpContext.RequestAborted);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.LastLoginMethod, loginMethod),
                ctx.HttpContext.RequestAborted);

        var roles = userEntity.UserRoles.Select(ur => ur.Role!.Name).ToList();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
            new(ClaimTypes.Name, userEntity.DisplayName),
            new(MercantecAuthClaims.LoginMethod, loginMethod),
        };
        foreach (var r in roles)
            claims.Add(new Claim(ClaimTypes.Role, r));

        if (!isLinkMode && await mfaGate.RequiresMfaStepAsync(userId, roles, loginMethod, ctx.HttpContext.RequestAborted))
            claims.Add(new Claim(MercantecAuthClaims.MfaPending, "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Principal = new ClaimsPrincipal(identity);

        if (!isLinkMode && claims.Any(c => c.Type == MercantecAuthClaims.MfaPending))
        {
            var original = ctx.Properties?.RedirectUri ?? "/";
            ctx.Properties ??= new AuthenticationProperties();
            ctx.Properties.RedirectUri =
                $"/Account/Mfa?returnUrl={Uri.EscapeDataString(original)}";
        }
    }

    private static bool TryGetAccountLinkTargetUserId(AuthenticationProperties? props, out Guid userId)
    {
        userId = default;
        if (props?.Items is null)
            return false;
        if (!props.Items.TryGetValue(AccountLinkAuthProperties.TargetUserIdKey, out var raw)
            || string.IsNullOrEmpty(raw))
            return false;

        return Guid.TryParse(raw, out userId);
    }
}
