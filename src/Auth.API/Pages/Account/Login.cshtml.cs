using Auth.API.Hosting;
using Auth.API.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class LoginModel(IConfiguration configuration, IOptions<AuthOptions> authOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool LocalLoginEnabled => authOptions.Value.EnableEmailPasswordLogin;

    public bool GoogleEnabled => !string.IsNullOrEmpty(configuration["OAuth:Google:ClientId"]);

    public bool MicrosoftEnabled => MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.WorkSection);

    public bool MicrosoftEduEnabled => MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.EduSection);

    public bool GitHubEnabled => !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]);

    public bool DiscordEnabled => !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]);

    public bool AnyOAuthProvider => GoogleEnabled || MicrosoftEnabled || MicrosoftEduEnabled || GitHubEnabled || DiscordEnabled;

    public string? ErrorMessage => Error switch
    {
        "invalid" => "Forkert e-mail eller adgangskode, eller ugyldig anmodning.",
        "no_password" => "Denne e-mail har endnu ikke adgangskode — log ind med Google/Microsoft m.fl. og tilføj adgangskode under Tilknyttede login, eller opret adgangskode med samme e-mail via Opret konto.",
        "disabled" => "Kontoen er deaktiveret.",
        "local_disabled" => "E-mail- og adgangskode-login er slået fra. Brug en af udbyderne ovenfor.",
        _ => null,
    };

    public string RegisterLink =>
        string.IsNullOrWhiteSpace(ReturnUrl)
            ? "/Account/Register"
            : $"/Account/Register?returnUrl={Uri.EscapeDataString(ReturnUrl)}";

    public string ChallengeUrl(string provider, string returnUrl, string? emailKind = null)
    {
        var q = $"provider={Uri.EscapeDataString(provider)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
        if (!string.IsNullOrEmpty(emailKind))
            q += $"&emailKind={Uri.EscapeDataString(emailKind)}";
        return $"/signin/challenge?{q}";
    }

    public void OnGet()
    {
    }
}
