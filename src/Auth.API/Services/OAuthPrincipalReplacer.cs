using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Hosting;
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
        var principal = ctx.Principal ?? throw new InvalidOperationException("Mangler principal efter OAuth.");
        var emailKind = OAuthEmailKindCookie.ReadAndClear(ctx.HttpContext);
        var userId = await sync.FindOrLinkUserAsync(principal, providerKey, emailKind, ctx.HttpContext.RequestAborted);
        var userEntity = await db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ctx.HttpContext.RequestAborted);

        var loginMethod = MercantecAuthClaims.ForOAuth(providerKey, emailKind);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.LastLoginMethod, loginMethod),
                ctx.HttpContext.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userEntity.Id.ToString()),
            new(ClaimTypes.Name, userEntity.DisplayName),
            new(MercantecAuthClaims.LoginMethod, loginMethod),
        };
        foreach (var ur in userEntity.UserRoles)
            claims.Add(new Claim(ClaimTypes.Role, ur.Role.Name));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        ctx.Principal = new ClaimsPrincipal(identity);
    }
}
