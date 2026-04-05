using System.Security.Claims;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Hosting;

public sealed class ConfigureJwtBearerOptions(
    IJwtSigningService signing,
    IOptions<JwtOptions> jwt) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt = jwt.Value;

    public void Configure(JwtBearerOptions options) =>
        Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (!string.Equals(name, JwtBearerDefaults.AuthenticationScheme, StringComparison.Ordinal))
            return;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signing.RsaKey,
            ValidIssuer = _jwt.Issuer,
            ValidAudience = _jwt.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name,
        };
    }
}
