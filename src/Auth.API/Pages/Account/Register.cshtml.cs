using Auth.API.Options;
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
        "email" => "E-mail er allerede registreret.",
        "invalid" => "Ugyldig retur-URL eller data.",
        "local_disabled" => "E-mail-oprettelse er slået fra i denne installation.",
        _ => null,
    };

    public string LoginLink =>
        string.IsNullOrWhiteSpace(ReturnUrl)
            ? "/Account/Login"
            : $"/Account/Login?returnUrl={Uri.EscapeDataString(ReturnUrl)}";

    public void OnGet()
    {
    }
}
