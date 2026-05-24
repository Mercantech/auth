using Auth.API.Options;
using Auth.API.Services;
using Microsoft.Extensions.Configuration;

namespace Auth.Tests.Unit;

public class ClientLoginMethodsPolicyTests
{
    private static IConfiguration ConfigWithOAuth(bool google = true, bool github = true, bool discord = true) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:Google:ClientId"] = google ? "g" : null,
                ["OAuth:GitHub:ClientId"] = github ? "gh" : null,
                ["OAuth:Discord:ClientId"] = discord ? "d" : null,
            })
            .Build();

    [Fact]
    public void ParseStored_normalizes_and_ignores_unknown()
    {
        var set = ClientLoginMethodCatalog.ParseStored("google, github, unknown, GOOGLE");
        Assert.Equal(2, set.Count);
        Assert.Contains("google", set);
        Assert.Contains("github", set);
    }

    [Fact]
    public void NormalizeStored_returns_null_for_empty()
    {
        Assert.Null(ClientLoginMethodCatalog.NormalizeStored([]));
        Assert.Equal("github,google", ClientLoginMethodCatalog.NormalizeStored(["google", "github"]));
    }

    [Fact]
    public void ProviderKeyToMethodId_maps_oauth_providers()
    {
        Assert.Equal("google", ClientLoginMethodCatalog.ProviderKeyToMethodId("google"));
        Assert.Equal("microsoft_edu", ClientLoginMethodCatalog.ProviderKeyToMethodId("microsoft-edu"));
        Assert.Null(ClientLoginMethodCatalog.ProviderKeyToMethodId("facebook"));
    }

    [Fact]
    public void Resolve_without_client_restriction_returns_global()
    {
        var auth = new AuthOptions { EnableEmailPasswordLogin = true };
        var policy = ClientLoginMethodsPolicy.Resolve(ConfigWithOAuth(), auth, "github", applyClientRestriction: false);
        Assert.True(policy.GitHub);
        Assert.True(policy.Google);
        Assert.True(policy.Password);
    }

    [Fact]
    public void Resolve_with_whitelist_intersects_global()
    {
        var auth = new AuthOptions { EnableEmailPasswordLogin = true };
        var policy = ClientLoginMethodsPolicy.Resolve(
            ConfigWithOAuth(github: true, discord: true),
            auth,
            "passkey,password,google",
            applyClientRestriction: true);

        Assert.True(policy.Passkey);
        Assert.True(policy.Password);
        Assert.True(policy.Google);
        Assert.False(policy.GitHub);
        Assert.False(policy.Discord);
    }

    [Fact]
    public void Resolve_null_whitelist_uses_all_global_when_restricted()
    {
        var auth = new AuthOptions { EnableEmailPasswordLogin = false };
        var policy = ClientLoginMethodsPolicy.Resolve(ConfigWithOAuth(), auth, null, applyClientRestriction: true);
        Assert.False(policy.Password);
        Assert.True(policy.Google);
    }
}
