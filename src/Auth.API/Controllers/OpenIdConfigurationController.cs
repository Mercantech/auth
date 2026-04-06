using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Auth.API.Controllers
{
    [AllowAnonymous]
    [EnableCors("MercantecSpa")]
    public sealed class OpenIdConfigurationController : ControllerBase
    {
        [HttpGet("/.well-known/openid-configuration")]
        [Produces("application/json")]
        public IActionResult Get()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');

            // Minimal OIDC discovery for internal SSO.
            var doc = new
            {
                issuer = baseUrl,
                authorization_endpoint = $"{baseUrl}/oauth/authorize",
                token_endpoint = $"{baseUrl}/oauth/token",
                jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                userinfo_endpoint = $"{baseUrl}/userinfo",

                response_types_supported = new[] { "code" },
                grant_types_supported = new[] { "authorization_code", "refresh_token" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "RS256" },
                scopes_supported = new[] { "openid", "profile", "email", "offline_access" },
                claims_supported = new[] { "sub", "name", "email", "role", "login_method", "iss", "aud", "exp", "iat" },
            };

            return Content(JsonSerializer.Serialize(doc), "application/json");
        }
    }
}

