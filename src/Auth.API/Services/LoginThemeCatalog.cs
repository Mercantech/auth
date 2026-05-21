namespace Auth.API.Services;

public static class LoginThemeCatalog
{
    public const string DefaultId = "mercantec";

    public static readonly LoginTheme Mercantec = new(
        Id: DefaultId,
        DisplayName: "Mercantec (standard)",
        Stylesheet: "/themes/mercantec.css",
        LogoUrl: null,
        PageTitleSuffix: "Mercantec Auth",
        TopbarTitle: "Mercantec Auth",
        LeadText: null);

    public static readonly LoginTheme Mercanlink = new(
        Id: "mercanlink",
        DisplayName: "Mercanlink",
        Stylesheet: "/themes/mercanlink.css",
        LogoUrl: "/themes/mercanlink/logo.png",
        PageTitleSuffix: "Mercanlink",
        TopbarTitle: "Mercanlink",
        LeadText: "Designet til moderne læringsmiljøer — log ind for at fortsætte");

    private static readonly IReadOnlyDictionary<string, LoginTheme> ById =
        new Dictionary<string, LoginTheme>(StringComparer.OrdinalIgnoreCase)
        {
            [Mercantec.Id] = Mercantec,
            [Mercanlink.Id] = Mercanlink,
        };

    /// <summary>Fallback når <see cref="ClientApp.LoginThemeId"/> ikke er sat i DB endnu.</summary>
    private static readonly IReadOnlyDictionary<string, string> DefaultThemeIdByClientId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mercanlink"] = Mercanlink.Id,
            ["Mercanlink-app"] = Mercanlink.Id,
        };

    public static IReadOnlyList<LoginTheme> All { get; } = [Mercantec, Mercanlink];

    public static LoginTheme Resolve(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ById.TryGetValue(themeId.Trim(), out var theme)
            ? theme
            : Mercantec;

    public static LoginTheme ResolveForClient(string? loginThemeId, string? clientId)
    {
        var fromDb = Resolve(loginThemeId);
        if (fromDb.Id != DefaultId || string.IsNullOrWhiteSpace(clientId))
            return fromDb;

        return DefaultThemeIdByClientId.TryGetValue(clientId.Trim(), out var presetId)
            ? Resolve(presetId)
            : Mercantec;
    }

    public static bool IsKnown(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ById.ContainsKey(themeId.Trim());

    public static string? NormalizeStored(string? themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId))
            return null;
        var t = themeId.Trim();
        return IsKnown(t) ? t : null;
    }
}
