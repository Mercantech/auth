using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Services;

public interface IJwtSigningService
{
    string KeyId { get; }
    RsaSecurityKey RsaKey { get; }
    Task EnsureKeysExistAsync(CancellationToken cancellationToken = default);
}
