using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Services
{
    public interface IOidcTokenService
    {
        string CreateIdToken(
            User user,
            string clientId,
            string? nonce,
            IEnumerable<string> scopes,
            string? authMethod,
            DateTime nowUtc,
            DateTime expiresUtc);
    }

    public sealed class OidcTokenService : IOidcTokenService
    {
        private readonly IJwtSigningService _signing;
        private readonly JwtOptions _jwt;

        public OidcTokenService(IJwtSigningService signing, IOptions<JwtOptions> jwtOptions)
        {
            _signing = signing;
            _jwt = jwtOptions.Value;
        }

        public string CreateIdToken(
            User user,
            string clientId,
            string? nonce,
            IEnumerable<string> scopes,
            string? authMethod,
            DateTime nowUtc,
            DateTime expiresUtc)
        {
            var scopeSet = new HashSet<string>(scopes.Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.Ordinal);
            var method = authMethod ?? user.LastLoginMethod ?? MercantecAuthClaims.LoginMethodValues.Unknown;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new(MercantecAuthClaims.LoginMethod, method),
            };

            if (scopeSet.Contains("profile"))
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, user.DisplayName));
            if (scopeSet.Contains("email") && !string.IsNullOrWhiteSpace(user.Email))
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));

            if (!string.IsNullOrWhiteSpace(nonce))
                claims.Add(new Claim("nonce", nonce));

            var creds = new SigningCredentials(_signing.RsaKey, SecurityAlgorithms.RsaSha256);
            var jwt = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: clientId,
                claims: claims,
                notBefore: nowUtc,
                expires: expiresUtc,
                signingCredentials: creds);
            jwt.Header["kid"] = _signing.KeyId;

            return new JwtSecurityTokenHandler { MapInboundClaims = false }.WriteToken(jwt);
        }
    }
}

