using OtpNet;

namespace Auth.Tests.Unit;

public class TotpVerificationTests
{
    [Fact]
    public void VerifyTotp_accepts_code_within_window()
    {
        const string secretBase32 = "JBSWY3DPEHPK3PXP";
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));
        var code = totp.ComputeTotp();

        var ok = totp.VerifyTotp(code, out _, new VerificationWindow(previous: 1, future: 1));

        Assert.True(ok);
    }

    [Fact]
    public void VerifyTotp_rejects_wrong_code()
    {
        const string secretBase32 = "JBSWY3DPEHPK3PXP";
        var totp = new Totp(Base32Encoding.ToBytes(secretBase32));

        var ok = totp.VerifyTotp("000000", out _, new VerificationWindow(previous: 1, future: 1));

        Assert.False(ok);
    }
}
