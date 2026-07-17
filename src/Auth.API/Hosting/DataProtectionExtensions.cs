using Microsoft.AspNetCore.DataProtection;

namespace Auth.API.Hosting;

public static class DataProtectionExtensions
{
    /// <summary>
    /// Persistér Data Protection-nøgler på disk, så auth-cookies overlever container-genstart/deploy.
    /// Uden dette genereres nye nøgler ved hvert start, og brugere bliver logget ud.
    /// </summary>
    public static IServiceCollection AddMercantecDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configured = configuration["DataProtection:KeysPath"];
        string keysPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            keysPath = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(environment.ContentRootPath, configured);
        }
        else
        {
            var jwtKeysDir = configuration["Jwt:KeysDirectory"] ?? "keys";
            keysPath = Path.Combine(environment.ContentRootPath, jwtKeysDir, "dataprotection");
        }

        Directory.CreateDirectory(keysPath);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("Mercantec.Auth");

        return services;
    }
}
