using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class RegisterModel(IOptions<AuthOptions> authOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool LocalLoginEnabled => authOptions.Value.EnableEmailPasswordLogin;

    public string? ErrorMessage => Error switch
    {
        "email" => "Der findes allerede adgangskode på denne e-mail. Log ind i stedet.",
        "invalid" => "Ugyldig retur-URL eller data.",
        "local_disabled" => "E-mail-oprettelse er slået fra i denne installation.",
        "disabled" => "Kontoen er deaktiveret.",
        _ => null,
    };

    public string LoginLink =>
        LoginBrandingUrls.Login(
            ReturnUrl,
            clientId: LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext));

    public void OnGet()
    {
    }
}
