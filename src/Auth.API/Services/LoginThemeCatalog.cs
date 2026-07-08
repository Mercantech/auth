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

    public static readonly LoginTheme Gf2Learn = new(
        Id: "gf2learn",
        DisplayName: "GF2 Learn",
        Stylesheet: "/themes/gf2learn.css",
        LogoUrl: "/themes/gf2learn/logo.svg",
        PageTitleSuffix: "GF2 Learn",
        TopbarTitle: "GF2 Learn",
        LeadText: "Grundforløb 2 programmering — log ind for at fortsætte til platformen");

    public static readonly LoginTheme UptimeDaddy = new(
        Id: "uptimedaddy",
        DisplayName: "Uptime Daddy",
        Stylesheet: "/themes/uptimedaddy.css",
        LogoUrl: "/themes/uptimedaddy/logo.png",
        PageTitleSuffix: "Uptime Daddy",
        TopbarTitle: "Uptime Daddy",
        LeadText: "Overvågning af oppetid — log ind for at fortsætte til dashboardet");

    private static readonly IReadOnlyDictionary<string, LoginTheme> ById =
        new Dictionary<string, LoginTheme>(StringComparer.OrdinalIgnoreCase)
        {
            [Mercantec.Id] = Mercantec,
            [Mercanlink.Id] = Mercanlink,
            [Gf2Learn.Id] = Gf2Learn,
            [UptimeDaddy.Id] = UptimeDaddy,
        };

    /// <summary>Fallback når <see cref="ClientApp.LoginThemeId"/> ikke er sat i DB endnu.</summary>
    private static readonly IReadOnlyDictionary<string, string> DefaultThemeIdByClientId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["mercanlink"] = Mercanlink.Id,
            ["Mercanlink-app"] = Mercanlink.Id,
            ["gf2-learn"] = Gf2Learn.Id,
            ["uptimedaddy"] = UptimeDaddy.Id,
            ["uptime-daddy"] = UptimeDaddy.Id,
        };

    public static IReadOnlyList<LoginTheme> All { get; } = [Mercantec, Mercanlink, Gf2Learn, UptimeDaddy];

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
