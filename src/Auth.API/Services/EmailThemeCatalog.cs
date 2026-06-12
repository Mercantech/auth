using Auth.API.Data;
using Auth.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public static class EmailThemeCatalog
{
    public static EmailThemePalette GetPalette(LoginTheme theme, string publicBaseUrl)
    {
        var baseUrl = publicBaseUrl.TrimEnd('/');
        return theme.Id switch
        {
            "mercanlink" => Mercanlink(theme, baseUrl),
            "gf2learn" => Gf2Learn(theme, baseUrl),
            _ => Mercantec(theme, baseUrl),
        };
    }

    public static async Task<EmailThemePalette> ResolveForClientAsync(
        AuthDbContext db,
        IOptions<EmailOptions> emailOptions,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        string? themeId = null;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var clientIdNorm = clientId.Trim();
            var app = await db.ClientApps
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.IsActive && EF.Functions.ILike(c.ClientId, clientIdNorm),
                    cancellationToken);
            themeId = app?.LoginThemeId;
        }

        var theme = LoginThemeCatalog.ResolveForClient(themeId, clientId);
        return GetPalette(theme, emailOptions.Value.PublicBaseUrl);
    }

    private static EmailThemePalette Mercantec(LoginTheme theme, string baseUrl) =>
        new(
            ThemeId: theme.Id,
            BrandTitle: theme.TopbarTitle,
            LogoAbsoluteUrl: $"{baseUrl}/brand/logo-mark.svg",
            Ink: "#161412",
            Muted: "#524a42",
            CardBg: "#f7f4ed",
            Edge: "#141210",
            Accent: "#2a4d5c",
            AccentHover: "#1f3a46",
            PageBg: "#e3ddd2",
            BrandMarkBg: "#2a4d5c",
            BrandMarkFg: "#e8f4f0",
            FontFamily: "'Plus Jakarta Sans', Segoe UI, Helvetica, Arial, sans-serif",
            TitleFontFamily: "'Syne', 'Plus Jakarta Sans', system-ui, sans-serif",
            CardRadius: "4px",
            ButtonRadius: "4px",
            ButtonUppercase: true,
            CardHardShadow: true);

    private static EmailThemePalette Mercanlink(LoginTheme theme, string baseUrl) =>
        new(
            ThemeId: theme.Id,
            BrandTitle: theme.TopbarTitle,
            LogoAbsoluteUrl: theme.LogoUrl is not null ? $"{baseUrl}{theme.LogoUrl}" : null,
            Ink: "#0f172a",
            Muted: "#64748b",
            CardBg: "#ffffff",
            Edge: "#e2e8f0",
            Accent: "#4f46e5",
            AccentHover: "#4338ca",
            PageBg: "#f1f5f9",
            BrandMarkBg: "#4f46e5",
            BrandMarkFg: "#ffffff",
            FontFamily: "'Plus Jakarta Sans', Segoe UI, Helvetica, Arial, sans-serif",
            TitleFontFamily: "'Plus Jakarta Sans', system-ui, sans-serif",
            CardRadius: "16px",
            ButtonRadius: "12px",
            ButtonUppercase: false,
            CardHardShadow: false);

    private static EmailThemePalette Gf2Learn(LoginTheme theme, string baseUrl) =>
        new(
            ThemeId: theme.Id,
            BrandTitle: theme.TopbarTitle,
            LogoAbsoluteUrl: theme.LogoUrl is not null ? $"{baseUrl}{theme.LogoUrl}" : null,
            Ink: "#0f172a",
            Muted: "#64748b",
            CardBg: "#ffffff",
            Edge: "#e2e8f0",
            Accent: "#0284c7",
            AccentHover: "#0369a1",
            PageBg: "#eef1f6",
            BrandMarkBg: "#0284c7",
            BrandMarkFg: "#ffffff",
            FontFamily: "'Plus Jakarta Sans', Segoe UI, Helvetica, Arial, sans-serif",
            TitleFontFamily: "'Plus Jakarta Sans', system-ui, sans-serif",
            CardRadius: "12px",
            ButtonRadius: "8px",
            ButtonUppercase: false,
            CardHardShadow: false);
}
