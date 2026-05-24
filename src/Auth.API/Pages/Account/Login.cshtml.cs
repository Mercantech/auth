using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class LoginModel(IConfiguration configuration, IOptions<AuthOptions> authOptions) : PageModel
{
    private ClientLoginMethodsPolicy _methods =
        ClientLoginMethodsPolicy.FromGlobalConfiguration(configuration, authOptions.Value);

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool PasskeyEnabled => _methods.Passkey;
    public bool LocalLoginEnabled => _methods.Password;
    public bool GoogleEnabled => _methods.Google;
    public bool MicrosoftEnabled => _methods.Microsoft;
    public bool MicrosoftEduEnabled => _methods.MicrosoftEdu;
    public bool GitHubEnabled => _methods.GitHub;
    public bool DiscordEnabled => _methods.Discord;
    public bool AnyOAuthProvider => _methods.AnyOAuthProvider;
    public bool AnyMethodEnabled => _methods.AnyMethod;

    public string? ErrorMessage => Error switch
    {
        "invalid" => "Forkert e-mail eller adgangskode, eller ugyldig anmodning.",
        "no_password" => "Denne e-mail har endnu ikke adgangskode — log ind med Google/Microsoft m.fl. og tilføj adgangskode under Tilknyttede login, eller opret adgangskode med samme e-mail via Opret konto.",
        "disabled" => "Kontoen er deaktiveret.",
        "local_disabled" => "E-mail- og adgangskode-login er slået fra. Brug en af udbyderne ovenfor.",
        "passkey" => "Passkey-login fejlede — registrér en passkey under Sikkerhed når du er logget ind.",
        "provider" => "Denne login-metode er ikke tilladt for denne app.",
        _ => null,
    };

    public string RegisterLink =>
        LoginBrandingUrls.Register(
            ReturnUrl,
            clientId: LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext));

    public string ChallengeUrl(string provider, string returnUrl, string? emailKind = null)
    {
        var q = $"provider={Uri.EscapeDataString(provider)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
        if (!string.IsNullOrEmpty(emailKind))
            q += $"&emailKind={Uri.EscapeDataString(emailKind)}";
        return $"/signin/challenge?{q}";
    }

    public void OnGet() => _methods = HttpContext.GetLoginMethods();
}
