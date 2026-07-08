using Auth.API.Services;

namespace Auth.Tests.Unit;

public class EmailThemeCatalogTests
{
    [Theory]
    [InlineData("mercantec", "#2a4d5c", true)]
    [InlineData("mercanlink", "#4f46e5", false)]
    [InlineData("gf2learn", "#0284c7", false)]
    public void GetPalette_matches_login_theme_colors(string themeId, string accent, bool hardShadow)
    {
        var theme = LoginThemeCatalog.Resolve(themeId);
        var palette = EmailThemeCatalog.GetPalette(theme, "https://auth.mercantec.tech");

        Assert.Equal(accent, palette.Accent);
        Assert.Equal(hardShadow, palette.CardHardShadow);
        Assert.Equal(theme.TopbarTitle, palette.BrandTitle);
        Assert.StartsWith("https://auth.mercantec.tech", palette.LogoAbsoluteUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPalette_mercanlink_uses_client_logo()
    {
        var theme = LoginThemeCatalog.Mercanlink;
        var palette = EmailThemeCatalog.GetPalette(theme, "https://auth.mercantec.tech");

        Assert.Equal("https://auth.mercantec.tech/themes/mercanlink/logo.png", palette.LogoAbsoluteUrl);
    }

    [Fact]
    public void GetPalette_uptimedaddy_uses_dark_palette_without_logo()
    {
        var theme = LoginThemeCatalog.UptimeDaddy;
        var palette = EmailThemeCatalog.GetPalette(theme, "https://auth.mercantec.tech");

        Assert.Equal("#408a71", palette.Accent);
        Assert.Equal("#0f1f1c", palette.CardBg);
        Assert.Equal("#091413", palette.PageBg);
        Assert.False(palette.CardHardShadow);
        Assert.Null(palette.LogoAbsoluteUrl);
        Assert.Equal("Uptime Daddy", palette.BrandTitle);
    }
}
