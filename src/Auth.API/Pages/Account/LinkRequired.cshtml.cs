using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Hosting;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Pages.Account;

[Authorize(Policy = MfaPolicies.FullSession)]
public class LinkRequiredModel(
    AuthDbContext db,
    IClientRequiredLinkService requiredLinkService,
    IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Error { get; set; }

    public string ClientDisplayName { get; private set; } = "appen";
    public IReadOnlyList<MissingLinkOffer> MissingProviders { get; private set; } = [];

    public string? ErrorMessage => Error switch
    {
        "link_conflict" => "Denne udbyder-konto tilhører allerede en anden Mercantec-bruger.",
        _ => null,
    };

    public sealed record MissingLinkOffer(string ProviderKey, string Name, string Hint, string Href);

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl)
            || !ClientLoginBrandingService.IsOAuthReturnUrl(ReturnUrl))
        {
            return Redirect("/Account/Security");
        }

        var clientId = LoginBrandingUrls.ClientIdFromReturnUrlOrCookie(ReturnUrl, HttpContext);
        if (string.IsNullOrWhiteSpace(clientId))
            return Redirect("/Account/Security");

        var client = await db.ClientApps
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsActive && EF.Functions.ILike(c.ClientId, clientId.Trim()));

        if (client is null || string.IsNullOrWhiteSpace(client.RequiredLinkedProviders))
            return LocalRedirect(ReturnUrl!);

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Redirect(LoginBrandingUrls.Login(ReturnUrl, clientId: clientId));

        var missing = await requiredLinkService.GetMissingMethodsAsync(
            userId,
            client.RequiredLinkedProviders,
            HttpContext.RequestAborted);

        if (missing.Count == 0)
            return LocalRedirect(ReturnUrl!);

        ClientDisplayName = client.Name;
        MissingProviders = missing
            .Select(m => BuildOffer(m.Id, m.DisplayName))
            .ToList();

        return Page();
    }

    private MissingLinkOffer BuildOffer(string methodId, string displayName)
    {
        var providerKey = ClientLoginMethodCatalog.MethodIdToLinkProviderKey(methodId);
        var hint = providerKey switch
        {
            "microsoft" => "Tilknyt Mercantec / Microsoft 365 arbejdskonto",
            "microsoft-edu" => "Tilknyt skolemail (@edu.mercantec.dk)",
            "google" => "Tilknyt Google-konto til din Mercantec-bruger",
            "github" => "Tilknyt GitHub-konto",
            "discord" => "Tilknyt Discord-konto",
            _ => "Tilknyt kontoen til din Mercantec-bruger",
        };

        var enabled = IsProviderConfigured(providerKey);
        var href = enabled
            ? $"{Request.PathBase}/account/link/start?provider={Uri.EscapeDataString(providerKey)}&returnUrl={Uri.EscapeDataString(ReturnUrl ?? "/")}"
            : "#";

        return new MissingLinkOffer(providerKey, displayName, hint, href);
    }

    private bool IsProviderConfigured(string providerKey) =>
        providerKey switch
        {
            "google" => !string.IsNullOrEmpty(configuration["OAuth:Google:ClientId"]),
            "microsoft" => MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.WorkSection),
            "microsoft-edu" => MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.EduSection),
            "github" => !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]),
            "discord" => !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]),
            _ => false,
        };

    public static string LinkTileCssModifiers(string providerKey) =>
        LinkedAccountsModel.LinkTileCssModifiers(providerKey);
}
