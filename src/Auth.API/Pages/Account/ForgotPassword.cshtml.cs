using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class ForgotPasswordModel(IOptions<AuthOptions> authOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool LocalLoginEnabled =>
        HttpContext.GetLoginMethods().Password && authOptions.Value.EnableEmailPasswordLogin;

    public string? ErrorMessage => Error switch
    {
        "sent" => "Hvis der findes en konto med den e-mail, har vi sendt et link til nulstilling af adgangskode.",
        "rate_limit" => "For mange forsøg. Vent et øjeblik og prøv igen.",
        "invalid" => "Ugyldig anmodning.",
        "local_disabled" => "E-mail-login er slået fra.",
        _ => null,
    };

    public string LoginLink =>
        LoginBrandingUrls.Login(
            ReturnUrl,
            clientId: LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext));
}
