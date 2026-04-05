using System.Security.Claims;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Auth.API.Services;

public static class SignInHelper
{
    public static async Task SignInAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        TimeSpan? expire = null,
        string loginMethod = MercantecAuthClaims.LoginMethodValues.Password)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(MercantecAuthClaims.LoginMethod, loginMethod),
        };
        foreach (var r in roleNames)
            claims.Add(new Claim(ClaimTypes.Role, r));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(expire ?? TimeSpan.FromDays(14)),
        };
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }
}
