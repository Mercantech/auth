using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.API.Pages.Account;

public class ResetPasswordModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public string? ErrorMessage => Error switch
    {
        "password_invalid" => "Adgangskoderne matcher ikke, eller opfylder ikke kravene (8–100 tegn).",
        "token_invalid" => "Linket er ugyldigt eller udløbet. Anmod om et nyt fra Glemt adgangskode.",
        "invalid" => "Ugyldig anmodning.",
        _ => null,
    };

    public string ForgotPasswordLink =>
        LoginBrandingUrls.ForgotPassword(clientId: LoginBrandingUrls.ClientIdFromContext(HttpContext));
}
