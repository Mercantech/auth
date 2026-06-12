using Auth.API.Hosting;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

public class ConfirmEmailSentModel(IOptions<AuthOptions> authOptions) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    public bool LocalLoginEnabled =>
        HttpContext.GetLoginMethods().Password && authOptions.Value.EnableEmailPasswordLogin;

    public string? StatusMessage => Status switch
    {
        "resent" => "Vi har sendt en ny bekræftelsesmail, hvis kontoen findes og ikke allerede er bekræftet.",
        "rate_limit" => "For mange forsøg. Vent et øjeblik og prøv igen.",
        "invalid" => "Ugyldig anmodning.",
        _ => "Vi har sendt en bekræftelsesmail. Klik på linket i mailen for at aktivere din konto.",
    };

    public string LoginLink =>
        LoginBrandingUrls.Login(
            ReturnUrl,
            clientId: LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext));
}
