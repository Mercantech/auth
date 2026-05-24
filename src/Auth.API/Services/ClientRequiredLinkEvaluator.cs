using Auth.API.Models;
using Auth.API.Models.Entities;

namespace Auth.API.Services;

public static class ClientRequiredLinkEvaluator
{
    public static bool UserHasLinkedProvider(User user, string methodId) =>
        methodId.Trim().ToLowerInvariant() switch
        {
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Google.Id, StringComparison.OrdinalIgnoreCase) =>
                HasExternalProvider(user, "google"),
            _ when string.Equals(methodId, ClientLoginMethodCatalog.GitHub.Id, StringComparison.OrdinalIgnoreCase) =>
                HasExternalProvider(user, "github"),
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Discord.Id, StringComparison.OrdinalIgnoreCase) =>
                HasExternalProvider(user, "discord"),
            _ when string.Equals(methodId, ClientLoginMethodCatalog.Microsoft.Id, StringComparison.OrdinalIgnoreCase) =>
                HasMicrosoftWorkLink(user),
            _ when string.Equals(methodId, ClientLoginMethodCatalog.MicrosoftEdu.Id, StringComparison.OrdinalIgnoreCase) =>
                HasMicrosoftSchoolLink(user),
            _ => false,
        };

    public static IReadOnlyList<string> GetMissing(User user, IEnumerable<string> requiredMethodIds) =>
        requiredMethodIds
            .Where(id => !UserHasLinkedProvider(user, id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool HasExternalProvider(User user, string provider) =>
        user.ExternalLogins.Any(e => string.Equals(e.Provider, provider, StringComparison.OrdinalIgnoreCase));

    private static bool HasMicrosoftWorkLink(User user) =>
        HasExternalProvider(user, "microsoft")
        && user.LinkedEmails.Any(e => e.Kind == UserEmailKind.Work);

    private static bool HasMicrosoftSchoolLink(User user) =>
        HasExternalProvider(user, "microsoft-edu")
        || (HasExternalProvider(user, "microsoft")
            && user.LinkedEmails.Any(e => e.Kind == UserEmailKind.School));
}
