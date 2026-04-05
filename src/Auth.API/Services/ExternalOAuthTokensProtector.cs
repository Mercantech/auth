using System.Text.Json;
using Auth.API.Models;
using Microsoft.AspNetCore.DataProtection;

namespace Auth.API.Services;

public sealed class ExternalOAuthTokensProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("Mercantec.Auth.ExternalOAuthTokens.v1");

    public string Protect(ExternalOAuthTokensPayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        return Convert.ToBase64String(_protector.Protect(json));
    }

    public ExternalOAuthTokensPayload? Unprotect(string? cipher)
    {
        if (string.IsNullOrWhiteSpace(cipher))
            return null;
        try
        {
            var json = _protector.Unprotect(Convert.FromBase64String(cipher));
            return JsonSerializer.Deserialize<ExternalOAuthTokensPayload>(json);
        }
        catch
        {
            return null;
        }
    }
}
