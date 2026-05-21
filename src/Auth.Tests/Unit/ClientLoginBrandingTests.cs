using Auth.API.Services;

namespace Auth.Tests.Unit;

public class ClientLoginBrandingTests
{
    [Theory]
    [InlineData("/oauth/authorize?client_id=mercanlink&redirect_uri=https%3A%2F%2Fapp.example%2Fcb", "mercanlink")]
    [InlineData("/oauth/authorize?response_type=code&client_id=demo", "demo")]
    [InlineData("/Account/Login", null)]
    public void TryParseClientIdFromReturnUrl_parses_oauth_query(string returnUrl, string? expected)
    {
        var actual = ClientLoginBrandingService.TryParseClientIdFromReturnUrl(returnUrl);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("/oauth/authorize?client_id=x", true)]
    [InlineData("/Account/Login", false)]
    [InlineData("/", false)]
    public void IsOAuthReturnUrl_detects_authorize_path(string? returnUrl, bool expected)
    {
        Assert.Equal(expected, ClientLoginBrandingService.IsOAuthReturnUrl(returnUrl));
    }

    [Fact]
    public void LoginThemeCatalog_normalizes_unknown_to_null()
    {
        Assert.Null(LoginThemeCatalog.NormalizeStored("not-a-theme"));
        Assert.Equal("mercanlink", LoginThemeCatalog.NormalizeStored("mercanlink"));
        Assert.Null(LoginThemeCatalog.NormalizeStored(""));
    }

    [Fact]
    public void ResolveForClient_maps_Mercanlink_app_without_db_theme()
    {
        var theme = LoginThemeCatalog.ResolveForClient(null, "Mercanlink-app");
        Assert.Equal("mercanlink", theme.Id);
        Assert.Equal("Mercanlink", theme.PageTitleSuffix);
    }

    [Fact]
    public void Login_url_includes_client_id()
    {
        var url = LoginBrandingUrls.Login("/oauth/authorize?client_id=mercanlink", clientId: "mercanlink");
        Assert.Contains("client_id=mercanlink", url, StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=", url, StringComparison.Ordinal);
    }
}
