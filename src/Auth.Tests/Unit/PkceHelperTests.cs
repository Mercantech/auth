using System.Security.Cryptography;
using System.Text;
using Auth.API.Security;

namespace Auth.Tests.Unit;

public class PkceHelperTests
{
    [Fact]
    public void VerifyS256_accepts_matching_verifier_and_challenge()
    {
        const string verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = PkceHelper.Base64UrlEncode(hash);

        Assert.True(PkceHelper.VerifyS256(verifier, challenge));
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("abc", "")]
    [InlineData(null, "abc")]
    [InlineData("abc", null)]
    public void VerifyS256_rejects_empty_inputs(string? verifier, string? challenge)
    {
        Assert.False(PkceHelper.VerifyS256(verifier ?? "", challenge ?? ""));
    }

    [Fact]
    public void VerifyS256_rejects_wrong_verifier()
    {
        const string verifier = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes("other"));
        var challenge = PkceHelper.Base64UrlEncode(hash);

        Assert.False(PkceHelper.VerifyS256(verifier, challenge));
    }

    [Fact]
    public void Base64UrlEncode_produces_unpadded_alphabet()
    {
        var data = new byte[] { 251, 255, 0 };
        var enc = PkceHelper.Base64UrlEncode(data);

        Assert.DoesNotContain('=', enc);
        Assert.DoesNotContain('+', enc);
        Assert.DoesNotContain('/', enc);
    }
}
