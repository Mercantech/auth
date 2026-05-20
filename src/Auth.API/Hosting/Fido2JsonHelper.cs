using System.Text.Json;
using Fido2NetLib;

namespace Auth.API.Hosting;

internal static class Fido2JsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<(AuthenticatorAttestationRawResponse? Attestation, string FriendlyName)> ReadRegistrationAsync(
        HttpRequest request)
    {
        using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
        var root = doc.RootElement;
        AuthenticatorAttestationRawResponse? attestation = null;
        if (root.TryGetProperty("attestation", out var el))
            attestation = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(el.GetRawText(), JsonOptions);

        var name = "Passkey";
        if (root.TryGetProperty("friendlyName", out var fn) && !string.IsNullOrWhiteSpace(fn.GetString()))
            name = fn.GetString()!;

        return (attestation, name);
    }

    public static async Task<(AuthenticatorAssertionRawResponse? Assertion, string? ReturnUrl, string? FriendlyName)> ReadAssertionBodyAsync(
        HttpRequest request)
    {
        using var doc = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
        var root = doc.RootElement;
        AuthenticatorAssertionRawResponse? assertion = null;
        if (root.TryGetProperty("assertion", out var a))
            assertion = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(a.GetRawText(), JsonOptions);

        string? returnUrl = null;
        if (root.TryGetProperty("returnUrl", out var r))
            returnUrl = r.GetString();

        string? friendlyName = null;
        if (root.TryGetProperty("friendlyName", out var f))
            friendlyName = f.GetString();

        return (assertion, returnUrl, friendlyName);
    }
}
