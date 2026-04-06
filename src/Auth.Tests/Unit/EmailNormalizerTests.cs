using Auth.API.Services;

namespace Auth.Tests.Unit;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("User@Example.COM", "user@example.com")]
    [InlineData("  A@B.CO  ", "a@b.co")]
    public void Normalize_trims_and_lowercases(string? input, string? expected)
    {
        Assert.Equal(expected, EmailNormalizer.Normalize(input));
    }
}
