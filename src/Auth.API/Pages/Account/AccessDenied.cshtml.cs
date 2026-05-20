using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Auth.API.Pages.Account;

public class AccessDeniedModel : PageModel
{
    public string? ReturnUrl { get; private set; }

    public void OnGet(string? returnUrl) =>
        ReturnUrl = returnUrl;
}
