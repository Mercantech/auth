using Auth.API.Options;

namespace Auth.Tests.Unit;

public class TokenLifetimePolicyTests
{
    private readonly JwtOptions _defaults = new()
    {
        AccessTokenExpiryMinutes = 15,
        RefreshTokenExpiryDays = 30,
    };

    [Fact]
    public void Resolve_uses_client_specific_values_when_configured()
    {
        Assert.Equal(480, TokenLifetimePolicy.ResolveAccessTokenMinutes(480, _defaults));
        Assert.Equal(60, TokenLifetimePolicy.ResolveRefreshTokenDays(60, _defaults));
    }

    [Fact]
    public void Resolve_uses_global_defaults_when_client_values_are_empty()
    {
        Assert.Equal(15, TokenLifetimePolicy.ResolveAccessTokenMinutes(null, _defaults));
        Assert.Equal(30, TokenLifetimePolicy.ResolveRefreshTokenDays(null, _defaults));
    }

    [Theory]
    [InlineData(1, TokenLifetimePolicy.MinimumAccessTokenMinutes)]
    [InlineData(2000, TokenLifetimePolicy.MaximumAccessTokenMinutes)]
    public void Access_token_lifetime_is_clamped_to_safe_range(int configured, int expected)
    {
        Assert.Equal(expected, TokenLifetimePolicy.ResolveAccessTokenMinutes(configured, _defaults));
    }

    [Theory]
    [InlineData(0, TokenLifetimePolicy.MinimumRefreshTokenDays)]
    [InlineData(500, TokenLifetimePolicy.MaximumRefreshTokenDays)]
    public void Refresh_token_lifetime_is_clamped_to_safe_range(int configured, int expected)
    {
        Assert.Equal(expected, TokenLifetimePolicy.ResolveRefreshTokenDays(configured, _defaults));
    }
}
