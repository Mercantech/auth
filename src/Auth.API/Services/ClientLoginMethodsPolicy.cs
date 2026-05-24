using Auth.API.Hosting;
using Auth.API.Options;

namespace Auth.API.Services;

public sealed record ClientLoginMethodsPolicy(
    bool Passkey,
    bool Password,
    bool Google,
    bool Microsoft,
    bool MicrosoftEdu,
    bool GitHub,
    bool Discord)
{
    public bool AnyOAuthProvider => Google || Microsoft || MicrosoftEdu || GitHub || Discord;

    public bool AnyMethod => Passkey || Password || AnyOAuthProvider;

    public bool IsAllowed(string methodId) =>
        methodId switch
        {
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Passkey.Id, StringComparison.OrdinalIgnoreCase) => Passkey,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Password.Id, StringComparison.OrdinalIgnoreCase) => Password,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Google.Id, StringComparison.OrdinalIgnoreCase) => Google,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Microsoft.Id, StringComparison.OrdinalIgnoreCase) => Microsoft,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.MicrosoftEdu.Id, StringComparison.OrdinalIgnoreCase) => MicrosoftEdu,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.GitHub.Id, StringComparison.OrdinalIgnoreCase) => GitHub,
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Discord.Id, StringComparison.OrdinalIgnoreCase) => Discord,
            _ => false,
        };

    public ClientLoginMethodsPolicy FilterTo(IReadOnlySet<string> allowedIds) =>
        new(
            Passkey && allowedIds.Contains(ClientLoginMethodCatalog.Passkey.Id),
            Password && allowedIds.Contains(ClientLoginMethodCatalog.Password.Id),
            Google && allowedIds.Contains(ClientLoginMethodCatalog.Google.Id),
            Microsoft && allowedIds.Contains(ClientLoginMethodCatalog.Microsoft.Id),
            MicrosoftEdu && allowedIds.Contains(ClientLoginMethodCatalog.MicrosoftEdu.Id),
            GitHub && allowedIds.Contains(ClientLoginMethodCatalog.GitHub.Id),
            Discord && allowedIds.Contains(ClientLoginMethodCatalog.Discord.Id));

    public static ClientLoginMethodsPolicy FromGlobalConfiguration(IConfiguration configuration, AuthOptions authOptions)
    {
        var google = !string.IsNullOrEmpty(configuration["OAuth:Google:ClientId"]);
        var microsoft = MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.WorkSection);
        var microsoftEdu = MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.EduSection);
        var github = !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]);
        var discord = !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]);
        var password = authOptions.EnableEmailPasswordLogin;

        return new ClientLoginMethodsPolicy(
            Passkey: true,
            Password: password,
            Google: google,
            Microsoft: microsoft,
            MicrosoftEdu: microsoftEdu,
            GitHub: github,
            Discord: discord);
    }

    public static ClientLoginMethodsPolicy Resolve(
        IConfiguration configuration,
        AuthOptions authOptions,
        string? clientAllowedLoginMethods,
        bool applyClientRestriction)
    {
        var global = FromGlobalConfiguration(configuration, authOptions);
        if (!applyClientRestriction)
            return global;

        var allowed = ClientLoginMethodCatalog.ParseStored(clientAllowedLoginMethods);
        if (allowed.Count == 0)
            return global;

        return global.FilterTo(allowed);
    }
}
