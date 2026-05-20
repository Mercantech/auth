using Microsoft.AspNetCore.Authentication.MicrosoftAccount;

namespace Auth.API.Hosting;

/// <summary>Authentication scheme-navne for eksterne login-udbydere.</summary>
public static class MercantecAuthSchemes
{
    /// <summary>Mercantec / arbejde — <c>OAuth:Microsoft</c>, callback <c>/signin-microsoft</c>.</summary>
    public const string MicrosoftWork = MicrosoftAccountDefaults.AuthenticationScheme;

    /// <summary>Skole (edu-tenant) — <c>OAuth:MicrosoftEDU</c>, callback <c>/signin-microsoft-edu</c>.</summary>
    public const string MicrosoftEdu = "MicrosoftEdu";
}
