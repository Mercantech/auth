using System.Text.Json;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Controllers;

[AllowAnonymous]
[EnableCors("MercantecSpa")]
public class JwksController(IJwtSigningService signing) : ControllerBase
{
    [HttpGet("/.well-known/jwks.json")]
    public IActionResult Get()
    {
        // RsaSecurityKey(RSA) sætter kun Rsa; Parameters er tom indtil eksplicit RSAParameters-konstruktør (IdentityModel 8.x).
        var key = signing.RsaKey;
        var crypto = key.Rsa ?? throw new InvalidOperationException("JWT signing key mangler RSA-reference.");
        var p = crypto.ExportParameters(includePrivateParameters: false);
        var n = Base64UrlEncoder.Encode(p.Modulus!);
        var e = Base64UrlEncoder.Encode(p.Exponent!);
        var doc = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = signing.KeyId,
                    alg = "RS256",
                    n,
                    e,
                },
            },
        };
        return Content(JsonSerializer.Serialize(doc), "application/json");
    }
}
