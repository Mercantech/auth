using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace Auth.API.Pages.Account;

[Authorize(Policy = MfaPolicies.FullSession)]
public class SecurityModel(
    ITotpMfaService totp,
    IPasskeyService passkeys) : PageModel
{
    public bool TotpEnabled { get; private set; }
    public string? SetupSecret { get; private set; }
    public string? SetupUri { get; private set; }
    public string? SetupQrDataUrl { get; private set; }
    public IReadOnlyList<PasskeyListItem> Passkeys { get; private set; } = [];
    public string? BannerMessage { get; private set; }
    public string? BannerError { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        string? handler,
        string? totp_enabled,
        string? totp_disabled,
        string? passkey_removed,
        string? error)
    {
        if (string.Equals(handler, "RegenerateTotp", StringComparison.OrdinalIgnoreCase))
            return RedirectToPage();
        BannerMessage = totp_enabled == "1" ? "Authenticator er aktiveret."
            : totp_disabled == "1" ? "Authenticator er deaktiveret."
            : passkey_removed == "1" ? "Passkey fjernet."
            : null;

        BannerError = error switch
        {
            "totp_confirm" => "Kunne ikke bekræfte TOTP — tjek koden.",
            "totp_disable" => "Forkert kode — TOTP ikke slået fra.",
            "totp_disable_passkey" => "Passkey-bekræftelse fejlede — authenticator er ikke slået fra.",
            "invalid_token" => "Ugyldig session.",
            _ => null,
        };

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return RedirectToPage("/Account/Login");

        TotpEnabled = await totp.IsEnabledAsync(userId);
        Passkeys = await passkeys.ListForUserAsync(userId);

        if (!TotpEnabled && string.IsNullOrEmpty(SetupSecret))
        {
            ApplySetup(await totp.BeginSetupAsync(userId));
        }

        return Page();
    }

    private void ApplySetup(TotpSetupResult setup)
    {
        SetupSecret = setup.SharedSecretBase32;
        SetupUri = setup.AuthenticatorUri;
        SetupQrDataUrl = TotpQrCodeHelper.ToPngDataUrl(setup.AuthenticatorUri);
    }
}
