namespace Auth.API.Services;

public sealed record ClientLoginMethod(string Id, string DisplayName, string Category);

public static class ClientLoginMethodCatalog
{
    public static readonly ClientLoginMethod Passkey = new("passkey", "Passkey", "Lokal");
    public static readonly ClientLoginMethod Password = new("password", "E-mail / adgangskode", "Lokal");
    public static readonly ClientLoginMethod Google = new("google", "Google", "Eksterne udbydere");
    public static readonly ClientLoginMethod Microsoft = new("microsoft", "Microsoft 365", "Eksterne udbydere");
    public static readonly ClientLoginMethod MicrosoftEdu = new("microsoft_edu", "Microsoft skolemail", "Eksterne udbydere");
    public static readonly ClientLoginMethod GitHub = new("github", "GitHub", "Eksterne udbydere");
    public static readonly ClientLoginMethod Discord = new("discord", "Discord", "Eksterne udbydere");

    private static readonly IReadOnlyDictionary<string, ClientLoginMethod> ById =
        new Dictionary<string, ClientLoginMethod>(StringComparer.OrdinalIgnoreCase)
        {
            [Passkey.Id] = Passkey,
            [Password.Id] = Password,
            [Google.Id] = Google,
            [Microsoft.Id] = Microsoft,
            [MicrosoftEdu.Id] = MicrosoftEdu,
            [GitHub.Id] = GitHub,
            [Discord.Id] = Discord,
        };

    public static IReadOnlyList<ClientLoginMethod> All { get; } =
        [Passkey, Password, Google, Microsoft, MicrosoftEdu, GitHub, Discord];

    public static bool IsKnown(string? id) =>
        !string.IsNullOrWhiteSpace(id) && ById.ContainsKey(id.Trim());

    public static string? NormalizeStored(IEnumerable<string>? ids)
    {
        if (ids is null)
            return null;

        var list = ids
            .Select(i => i.Trim())
            .Where(i => IsKnown(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(i => i, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return list.Count == 0 ? null : string.Join(',', list);
    }

    public static HashSet<string> ParseStored(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in raw.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsKnown(part))
                set.Add(ById[part].Id);
        }

        return set;
    }

    public static string? ProviderKeyToMethodId(string providerKey) =>
        providerKey.Trim().ToLowerInvariant() switch
        {
            "google" => Google.Id,
            "microsoft" => Microsoft.Id,
            "microsoft-edu" or "microsoftedu" => MicrosoftEdu.Id,
            "github" => GitHub.Id,
            "discord" => Discord.Id,
            _ => null,
        };
}
