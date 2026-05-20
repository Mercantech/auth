using System.Reflection;
using Auth.API.Services;

namespace Auth.Tests.Unit;

public class RecoveryCodeGenerationTests
{
    [Fact]
    public void CreateRecoveryCode_returns_ten_characters()
    {
        var method = typeof(TotpMfaService).GetMethod(
            "CreateRecoveryCode",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        for (var i = 0; i < 20; i++)
        {
            var code = (string)method.Invoke(null, null)!;
            Assert.Equal(10, code.Length);
            Assert.Matches("^[A-Z2-9]{10}$", code);
        }
    }
}
