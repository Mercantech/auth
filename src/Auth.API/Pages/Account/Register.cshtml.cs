using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class RegisterModel(IOptions<AuthOptions> authOptions, IConfiguration configuration) : PageModel
{
    private ClientLoginMethodsPolicy _methods =
        ClientLoginMethodsPolicy.FromGlobalConfiguration(configuration, authOptions.Value);

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool LocalLoginEnabled => _methods.Password;

    public string? ErrorMessage => Error switch
    {
        "email" => "Der findes allerede adgangskode på denne e-mail. Log ind i stedet.",
        "invalid" => "Ugyldig retur-URL eller data.",
        "local_disabled" => "E-mail-oprettelse er slået fra for denne app.",
        "disabled" => "Kontoen er deaktiveret.",
        "provider" => "Denne login-metode er ikke tilladt for denne app.",
        _ => null,
    };

    public string LoginLink =>
        LoginBrandingUrls.Login(
            ReturnUrl,
            clientId: LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext));

    public void OnGet() => _methods = HttpContext.GetLoginMethods();
}
