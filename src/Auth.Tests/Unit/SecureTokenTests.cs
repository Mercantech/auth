using Auth.API.Security;

namespace Auth.Tests.Unit;

public class SecureTokenTests
{
    [Fact]
    public void CreateOpaqueToken_returns_url_safe_string_of_expected_entropy_length()
    {
        var t = SecureToken.CreateOpaqueToken(32);

        Assert.InRange(t.Length, 40, 55);
        Assert.DoesNotContain('+', t);
        Assert.DoesNotContain('/', t);
        Assert.DoesNotContain('=', t);
    }

    [Fact]
    public void HashOpaqueToken_is_deterministic_hex()
    {
        const string token = "test-opaque-value";

        var h1 = SecureToken.HashOpaqueToken(token);
        var h2 = SecureToken.HashOpaqueToken(token);

        Assert.Equal(64, h1.Length);
        Assert.True(h1.All(Uri.IsHexDigit));
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void HashOpaqueToken_differs_for_different_tokens()
    {
        var a = SecureToken.HashOpaqueToken("a");
        var b = SecureToken.HashOpaqueToken("b");
        Assert.NotEqual(a, b);
    }
}
