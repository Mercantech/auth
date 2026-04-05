using System.Security.Cryptography;
using Auth.API.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Services;

public class JwtSigningService(IOptions<JwtOptions> options, IWebHostEnvironment env, ILogger<JwtSigningService> log) : IJwtSigningService, IDisposable
{
    private readonly JwtOptions _opt = options.Value;
    private RSA? _rsa;
    private RsaSecurityKey? _key;
    private readonly string _kid = "mercantec-auth-1";

    public string KeyId => _kid;
    public RsaSecurityKey RsaKey => _key ?? throw new InvalidOperationException("JWT keys not loaded. Call EnsureKeysExistAsync first.");

    public async Task EnsureKeysExistAsync(CancellationToken cancellationToken = default)
    {
        var keysDir = Path.GetFullPath(Path.Combine(env.ContentRootPath, _opt.KeysDirectory));
        Directory.CreateDirectory(keysDir);
        var privatePath = Path.Combine(keysDir, _opt.PrivateKeyFileName);
        var publicPath = Path.Combine(keysDir, _opt.PublicKeyFileName);

        if (!File.Exists(privatePath) || !File.Exists(publicPath))
        {
            log.LogWarning("Generating new RSA key pair in {KeysDir}", keysDir);
            using var rsaGen = RSA.Create(2048);
            var privatePem = rsaGen.ExportPkcs8PrivateKeyPem();
            var publicPem = rsaGen.ExportSubjectPublicKeyInfoPem();
            await File.WriteAllTextAsync(privatePath, privatePem, cancellationToken);
            await File.WriteAllTextAsync(publicPath, publicPem, cancellationToken);
        }

        _rsa?.Dispose();
        _rsa = RSA.Create();
        _rsa.ImportFromPem(await File.ReadAllTextAsync(privatePath, cancellationToken));
        _key = new RsaSecurityKey(_rsa) { KeyId = _kid };
    }

    public void Dispose() => _rsa?.Dispose();
}
