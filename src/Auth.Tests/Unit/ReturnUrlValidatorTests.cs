using Auth.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Auth.Tests.Unit;

public class ReturnUrlValidatorTests
{
    private static IConfiguration ConfigForSpa(params string[] origins)
    {
        var dict = new Dictionary<string, string?>();
        for (var i = 0; i < origins.Length; i++)
            dict[$"Cors:SpaOrigins:{i}"] = origins[i];
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static HttpRequest Req(string host = "auth.example.test", string scheme = "https")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Scheme = scheme;
        ctx.Request.Host = new HostString(host);
        return ctx.Request;
    }

    [Fact]
    public void IsValidOAuthAuthorizeReturnUrl_accepts_same_host_authorize_path()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req("login.example.com");

        Assert.True(v.IsValidOAuthAuthorizeReturnUrl("https://login.example.com/oauth/authorize?x=1", req));
    }

    [Fact]
    public void IsValidOAuthAuthorizeReturnUrl_rejects_different_host()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req("login.example.com");

        Assert.False(v.IsValidOAuthAuthorizeReturnUrl("https://evil.example.com/oauth/authorize", req));
    }

    [Fact]
    public void IsValidOAuthAuthorizeReturnUrl_accepts_relative_authorize()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req();

        Assert.True(v.IsValidOAuthAuthorizeReturnUrl("/oauth/authorize?client_id=demo", req));
    }

    [Fact]
    public void IsValidOAuthAuthorizeReturnUrl_rejects_relative_non_authorize()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req();

        Assert.False(v.IsValidOAuthAuthorizeReturnUrl("/Account/Login", req));
    }

    [Fact]
    public void IsSafePostLoginReturnUrl_accepts_relative_paths()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req();

        Assert.True(v.IsSafePostLoginReturnUrl("/", req));
        Assert.True(v.IsSafePostLoginReturnUrl("/Admin", req));
    }

    [Fact]
    public void IsSafePostLoginReturnUrl_rejects_protocol_relative()
    {
        var v = new ReturnUrlValidator(ConfigForSpa());
        var req = Req();

        Assert.False(v.IsSafePostLoginReturnUrl("//evil.com/phish", req));
    }

    [Fact]
    public void IsSafePostLogoutRedirectUrl_accepts_whitelisted_spa_origin()
    {
        var v = new ReturnUrlValidator(ConfigForSpa("http://localhost:5173"));
        var req = Req("auth.local.test", "http");

        Assert.True(v.IsSafePostLogoutRedirectUrl("http://localhost:5173/app", req));
    }

    [Fact]
    public void IsSafePostLogoutRedirectUrl_rejects_unknown_absolute_origin()
    {
        var v = new ReturnUrlValidator(ConfigForSpa("http://localhost:5173"));
        var req = Req();

        Assert.False(v.IsSafePostLogoutRedirectUrl("https://evil.example/", req));
    }

    [Fact]
    public void IsSafePostLogoutRedirectUrl_rejects_empty()
    {
        var v = new ReturnUrlValidator(ConfigForSpa("http://localhost:5173"));
        var req = Req();

        Assert.False(v.IsSafePostLogoutRedirectUrl("", req));
    }
}
