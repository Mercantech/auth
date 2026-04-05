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

    public bool MicrosoftEnabled => !string.IsNullOrEmpty(configuration["OAuth:Microsoft:ClientId"]);

    public bool GitHubEnabled => !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]);

    public bool DiscordEnabled => !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]);

    public bool AnyOAuthProvider => GoogleEnabled || MicrosoftEnabled || GitHubEnabled || DiscordEnabled;

    public string? ErrorMessage => Error switch
    {
        "invalid" => "Forkert e-mail eller adgangskode, eller ugyldig anmodning.",
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
