using Microsoft.AspNetCore.DataProtection;

namespace Auth.API.Services;

public sealed class TotpSecretProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Mercantec.Auth.TotpSecret.v1");

    public string Protect(string plainSecret) =>
        Convert.ToBase64String(_protector.Protect(System.Text.Encoding.UTF8.GetBytes(plainSecret)));

    public string Unprotect(string cipher)
    {
        var bytes = _protector.Unprotect(Convert.FromBase64String(cipher));
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
