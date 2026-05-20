namespace Auth.API.Services;

public sealed record LoginTheme(
    string Id,
    string DisplayName,
    string Stylesheet,
    string? LogoUrl,
    string PageTitleSuffix,
    string TopbarTitle,
    string? LeadText);
