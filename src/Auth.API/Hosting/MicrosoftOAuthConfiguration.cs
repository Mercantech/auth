using Auth.API.Security;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;

namespace Auth.API.Hosting;

/// <summary>Fælles konfiguration for Azure AD / Microsoft identity (arbejde + edu).</summary>
public static class MicrosoftOAuthConfiguration
{
    public const string WorkSection = "OAuth:Microsoft";
    public const string EduSection = "OAuth:MicrosoftEDU";

    public const string DefaultScope =
        "offline_access openid profile email https://graph.microsoft.com/User.Read";

    private static readonly string[] MultiTenantKeywords = ["common", "organizations", "consumers"];

    public static bool IsConfigured(IConfiguration configuration, string section) =>
        !string.IsNullOrWhiteSpace(configuration[$"{section}:ClientId"]);

    /// <summary>Vælg config-sektion til token refresh ud fra gemt login-metode.</summary>
    public static string SectionForAuthMethod(string? authMethod) =>
        string.Equals(authMethod, MercantecAuthClaims.LoginMethodValues.MicrosoftSchool, StringComparison.Ordinal)
            ? EduSection
            : WorkSection;

    public static void Apply(MicrosoftAccountOptions options, IConfiguration configuration, string section)
    {
        options.ClientId = configuration[$"{section}:ClientId"] ?? "";
        options.ClientSecret = configuration[$"{section}:ClientSecret"] ?? "";
        options.SaveTokens = true;

        options.Scope.Clear();
        var scopeLine = configuration[$"{section}:Scope"];
        if (string.IsNullOrWhiteSpace(scopeLine))
            scopeLine = DefaultScope;
        foreach (var part in scopeLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            options.Scope.Add(part);

        var tenant = configuration[$"{section}:TenantId"];
        if (!string.IsNullOrWhiteSpace(tenant)
            && !MultiTenantKeywords.Any(k => string.Equals(k, tenant, StringComparison.OrdinalIgnoreCase)))
        {
            options.AuthorizationEndpoint = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize";
            options.TokenEndpoint = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";
        }
    }

    public static string ResolveTokenEndpoint(IConfiguration configuration, string section)
    {
        var tenant = configuration[$"{section}:TenantId"];
        if (string.IsNullOrWhiteSpace(tenant)
            || MultiTenantKeywords.Any(k => string.Equals(k, tenant, StringComparison.OrdinalIgnoreCase)))
            return "https://login.microsoftonline.com/common/oauth2/v2.0/token";
        return $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token";
    }

    public static string ResolveScope(IConfiguration configuration, string section) =>
        configuration[$"{section}:Scope"] ?? DefaultScope;
}
