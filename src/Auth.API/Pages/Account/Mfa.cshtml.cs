using Auth.API.Data;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Pages.Account;

[Authorize(Policy = MfaPolicies.MfaStep)]
public class MfaModel(AuthDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public bool HasPasskeys { get; private set; }

    public string? ErrorMessage => Error switch
    {
        "invalid" => "Ugyldig kode. Prøv igen.",
        "invalid_token" => "Session udløbet — log ind igen.",
        _ => null,
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (!SignInHelper.IsMfaPending(User))
            return Redirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl!);

        ReturnUrl ??= "/";
        if (!Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            return RedirectToPage("/Account/Login");

        HasPasskeys = await db.UserPasskeyCredentials.AnyAsync(c => c.UserId == userId);
        return Page();
    }
}
