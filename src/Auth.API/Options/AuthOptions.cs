namespace Auth.API.Options;

/// <summary>Generelle auth-indstillinger (miljø / appsettings / Auth__* env vars).</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Når false: skjul e-mail+adgangskode på login/register og afvis <c>POST /signin</c> og <c>POST /signup</c>.
    /// OAuth-udbydere påvirkes ikke.
    /// </summary>
    public bool EnableEmailPasswordLogin { get; set; } = true;
}
