using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers
{
    [AllowAnonymous]
    [EnableCors("MercantecSpa")]
    public sealed class OidcLogoutController : ControllerBase
    {
        private readonly IReturnUrlValidator _urls;

        public OidcLogoutController(IReturnUrlValidator urls) => _urls = urls;

        /// <summary>
        /// OIDC end-session (minimal). Vi beholder også /signout for bagudkompatibilitet.
        /// </summary>
        [HttpGet("/connect/endsession")]
        public async Task<IActionResult> EndSession([FromQuery] string? post_logout_redirect_uri, [FromQuery] string? state)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (string.IsNullOrWhiteSpace(post_logout_redirect_uri)
                || !_urls.IsSafePostLogoutRedirectUrl(post_logout_redirect_uri, Request))
            {
                return Redirect("/");
            }

            if (string.IsNullOrWhiteSpace(state))
                return Redirect(post_logout_redirect_uri);

            var sep = post_logout_redirect_uri.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return Redirect(post_logout_redirect_uri + sep + "state=" + Uri.EscapeDataString(state));
        }
    }
}

