using System.Security.Claims;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public static class SignInHelper
{
    /// <returns>Relativ redirect-URL til MFA, eller null når fuld session er etableret.</returns>
    public static async Task<string?> EstablishSessionAfterPrimaryAuthAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        string loginMethod,
        string returnUrl,
        IMfaGateService mfaGate,
        IOptions<MfaOptions> mfaOptions,
        IEnumerable<string>? primaryAmr = null)
    {
        var amr = primaryAmr?.ToList() ?? [MercantecAuthClaims.AmrValues.Password];
        if (loginMethod != MercantecAuthClaims.LoginMethodValues.Password
            && loginMethod != MercantecAuthClaims.LoginMethodValues.Passkey)
        {
            amr = [MapLoginMethodToAmr(loginMethod)];
        }

        if (await mfaGate.RequiresMfaStepAsync(user.Id, roleNames, loginMethod, http.RequestAborted))
        {
            await SignInPendingAsync(http, user, roleNames, loginMethod, mfaOptions.Value.PendingSessionMinutes);
            var clientId = ClientLoginBrandingService.TryParseClientIdFromReturnUrl(returnUrl)
                ?? LoginBrandingUrls.ClientIdFromContext(http);
            return LoginBrandingUrls.Mfa(returnUrl, string.IsNullOrEmpty(clientId) ? null : clientId);
        }

        await SignInFullAsync(http, user, roleNames, loginMethod, amr);
        return null;
    }

    public static async Task SignInPendingAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        string loginMethod,
        int pendingMinutes = 10)
    {
        var claims = BuildBaseClaims(user, roleNames, loginMethod);
        claims.Add(new Claim(MercantecAuthClaims.MfaPending, "true"));

        await SignInWithClaimsAsync(http, claims, TimeSpan.FromMinutes(pendingMinutes));
    }

    public static async Task SignInFullAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        string loginMethod,
        IReadOnlyList<string> amrValues)
    {
        var claims = BuildBaseClaims(user, roleNames, loginMethod);
        foreach (var amr in amrValues.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim(MercantecAuthClaims.Amr, amr));

        await SignInWithClaimsAsync(http, claims, TimeSpan.FromDays(14));
    }

    /// <summary>Opgrader pending session til fuld efter MFA.</summary>
    public static async Task CompleteMfaAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        string loginMethod,
        string mfaAmr)
    {
        var existingAmr = http.User.FindAll(MercantecAuthClaims.Amr).Select(c => c.Value).ToList();
        if (existingAmr.Count == 0)
            existingAmr.Add(MapLoginMethodToAmr(loginMethod));

        if (!existingAmr.Contains(mfaAmr, StringComparer.Ordinal))
            existingAmr.Add(mfaAmr);

        await SignInFullAsync(http, user, roleNames, loginMethod, existingAmr);
    }

    public static bool IsMfaPending(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(MercantecAuthClaims.MfaPending), "true", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> GetAmrValues(ClaimsPrincipal user) =>
        user.FindAll(MercantecAuthClaims.Amr).Select(c => c.Value).ToList();

    private static List<Claim> BuildBaseClaims(User user, IReadOnlyList<string> roleNames, string loginMethod)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(MercantecAuthClaims.LoginMethod, loginMethod),
        };
        foreach (var r in roleNames)
            claims.Add(new Claim(ClaimTypes.Role, r));
        return claims;
    }

    private static async Task SignInWithClaimsAsync(
        HttpContext http,
        List<Claim> claims,
        TimeSpan expire)
    {
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(expire),
        };
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);
    }

    private static string MapLoginMethodToAmr(string loginMethod) =>
        loginMethod switch
        {
            MercantecAuthClaims.LoginMethodValues.Password => MercantecAuthClaims.AmrValues.Password,
            MercantecAuthClaims.LoginMethodValues.Passkey => MercantecAuthClaims.AmrValues.WebAuthn,
            _ => loginMethod,
        };

    [Obsolete("Use EstablishSessionAfterPrimaryAuthAsync or SignInFullAsync")]
    public static Task SignInAsync(
        HttpContext http,
        User user,
        IReadOnlyList<string> roleNames,
        TimeSpan? expire = null,
        string loginMethod = MercantecAuthClaims.LoginMethodValues.Password) =>
        SignInFullAsync(
            http,
            user,
            roleNames,
            loginMethod,
            [MercantecAuthClaims.AmrValues.Password]);
}
