using Auth.API.Models;

namespace Auth.API.Security;

/// <summary>Custom JWT- og cookie-claims for Mercantec Auth.</summary>
public static class MercantecAuthClaims
{
    /// <summary>Hvordan brugeren sidst autentificerede sig (fx <c>microsoft-work</c>, <c>password</c>).</summary>
    public const string LoginMethod = "login_method";

    public static class LoginMethodValues
    {
        public const string Password = "password";
        public const string Unknown = "unknown";
        public const string Google = "google";
        public const string GitHub = "github";
        public const string Discord = "discord";
        public const string Microsoft = "microsoft";
        public const string MicrosoftWork = "microsoft-work";
        public const string MicrosoftSchool = "microsoft-school";
    }

    /// <summary>OAuth provider key (google, microsoft, github, discord) + Microsoft e-mail-slags.</summary>
    public static string ForOAuth(string providerKey, UserEmailKind microsoftEmailKind)
    {
        if (string.Equals(providerKey, "microsoft", StringComparison.OrdinalIgnoreCase))
        {
            return microsoftEmailKind switch
            {
                UserEmailKind.Work => LoginMethodValues.MicrosoftWork,
                UserEmailKind.School => LoginMethodValues.MicrosoftSchool,
                _ => LoginMethodValues.Microsoft,
            };
        }

        return providerKey.ToLowerInvariant() switch
        {
            "google" => LoginMethodValues.Google,
            "github" => LoginMethodValues.GitHub,
            "discord" => LoginMethodValues.Discord,
            _ => providerKey.ToLowerInvariant(),
        };
    }
}
