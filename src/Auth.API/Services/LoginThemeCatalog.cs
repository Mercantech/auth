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
        LogoUrl: "/themes/mercanlink/logo.svg",
        PageTitleSuffix: "Mercanlink",
        TopbarTitle: "Mercanlink",
        LeadText: "Log ind for at fortsætte til Mercanlink");

    private static readonly IReadOnlyDictionary<string, LoginTheme> ById =
        new Dictionary<string, LoginTheme>(StringComparer.OrdinalIgnoreCase)
        {
            [Mercantec.Id] = Mercantec,
            [Mercanlink.Id] = Mercanlink,
        };

    public static IReadOnlyList<LoginTheme> All { get; } = [Mercantec, Mercanlink];

    public static LoginTheme Resolve(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ById.TryGetValue(themeId.Trim(), out var theme)
            ? theme
            : Mercantec;

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
