namespace Auth.API.Services;

/// <summary>Inline-sikre farver og typografi til e-mail — spejler login-temaerne.</summary>
public sealed record EmailThemePalette(
    string ThemeId,
    string BrandTitle,
    string? LogoAbsoluteUrl,
    string Ink,
    string Muted,
    string CardBg,
    string Edge,
    string Accent,
    string AccentHover,
    string PageBg,
    string BrandMarkBg,
    string BrandMarkFg,
    string FontFamily,
    string TitleFontFamily,
    string CardRadius,
    string ButtonRadius,
    bool ButtonUppercase,
    bool CardHardShadow)
{
    public IReadOnlyDictionary<string, string> ToTemplateValues() => new Dictionary<string, string>
    {
        ["themeId"] = ThemeId,
        ["brandTitle"] = BrandTitle,
        ["logoUrl"] = LogoAbsoluteUrl ?? string.Empty,
        ["hasLogo"] = LogoAbsoluteUrl is not null ? "true" : "false",
        ["ink"] = Ink,
        ["muted"] = Muted,
        ["cardBg"] = CardBg,
        ["edge"] = Edge,
        ["accent"] = Accent,
        ["accentHover"] = AccentHover,
        ["pageBg"] = PageBg,
        ["brandMarkBg"] = BrandMarkBg,
        ["brandMarkFg"] = BrandMarkFg,
        ["fontFamily"] = FontFamily,
        ["titleFontFamily"] = TitleFontFamily,
        ["cardRadius"] = CardRadius,
        ["buttonRadius"] = ButtonRadius,
        ["buttonTextTransform"] = ButtonUppercase ? "uppercase" : "none",
        ["buttonLetterSpacing"] = ButtonUppercase ? "0.06em" : "0.01em",
        ["cardBorder"] = CardHardShadow ? $"2px solid {Edge}" : $"1px solid {Edge}",
        ["cardShadow"] = CardHardShadow
            ? $"5px 5px 0 rgba(20, 18, 16, 0.14)"
            : "0 10px 30px -8px rgba(15, 23, 42, 0.12)",
        ["buttonBorder"] = CardHardShadow ? $"2px solid {Edge}" : "none",
        ["buttonShadow"] = CardHardShadow ? $"3px 3px 0 {Edge}" : "0 4px 14px -2px rgba(79, 70, 229, 0.35)",
        ["logoBlock"] = BuildLogoBlock(),
    };

    private string BuildLogoBlock()
    {
        if (LogoAbsoluteUrl is not null)
        {
            var imgStyle = CardHardShadow
                ? $"display:block;border-radius:{CardRadius};border:2px solid {Edge};box-shadow:4px 4px 0 {Edge};"
                : $"display:block;border-radius:{CardRadius};box-shadow:0 4px 14px -2px rgba(15,23,42,0.15);";
            return $"""<img src="{LogoAbsoluteUrl}" alt="" width="48" height="48" style="{imgStyle}" />""";
        }

        return $"""
            <div style="width:48px;height:48px;border-radius:{CardRadius};background:{BrandMarkBg};color:{BrandMarkFg};font-family:{TitleFontFamily};font-size:18px;font-weight:800;line-height:48px;text-align:center;{(CardHardShadow ? $"border:2px solid {Edge};box-shadow:4px 4px 0 {Edge};" : "")}">
              {char.ToUpperInvariant(BrandTitle[0])}
            </div>
            """;
    }
}
