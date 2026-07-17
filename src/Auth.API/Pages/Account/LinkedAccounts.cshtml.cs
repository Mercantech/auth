using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Hosting;
using Auth.API.Security;
using Auth.API.Models;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Pages.Account;

[Authorize(Policy = MfaPolicies.FullSession)]
public class LinkedAccountsModel(
    AuthDbContext db,
    IConfiguration configuration,
    IOptions<AuthOptions> authOptions,
    IOptions<DokployOptions> dokployOptions) : PageModel
{
    public IReadOnlyList<ExternalLoginRow> ExternalLogins { get; private set; } = [];
    public IReadOnlyList<LinkProviderOffer> OfferedLinkProviders { get; private set; } = [];

    /// <summary>Relativ eller absolut return-sti til brug i link/remove forms.</summary>
    public string ReturnUrlParam { get; private set; } = "/Account/LinkedAccounts";

    public bool HasPasswordLogin { get; private set; }

    public bool CanSetPassword => authOptions.Value.EnableEmailPasswordLogin && !HasPasswordLogin;

    /// <summary>Foreslået e-mail til adgangskode-formular (profil eller Personal UserEmail).</summary>
    public string? SuggestedPasswordEmail { get; private set; }

    public string? UserEmail { get; private set; }

    public string? BannerError { get; private set; }
    public bool ShowUnlinkedOk { get; private set; }
    public bool ShowPasswordSetOk { get; private set; }
    public bool ShowDokployCreatedOk { get; private set; }
    public bool ShowDokployLinkedOk { get; private set; }
    public bool ShowDokployAlreadyOk { get; private set; }

    public bool DokploySectionVisible { get; private set; }
    public bool DokployIsProvisioned { get; private set; }
    public string? DokployUserId { get; private set; }
    public string? DokployLastError { get; private set; }
    public string DokployUiUrl { get; private set; } = "https://deploy.mags.dk";

    public sealed record ExternalLoginRow(Guid Id, string Provider, string ProviderLabel, string? ProviderEmail, DateTime LinkedAtUtc);

    public sealed record LinkProviderOffer(string ProviderKey, string Name, string Hint, string Href);

    private static readonly TimeZoneInfo DanishTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");

    public async Task OnGetAsync(string? error, string? unlinked, string? password_set, string? dokploy)
    {
        ReturnUrlParam = Request.Path.HasValue ? Request.PathBase + Request.Path : "/Account/LinkedAccounts";

        BannerError = error switch
        {
            "link_conflict" => "Denne udbyder-konto tilhører allerede en anden Mercantec-bruger.",
            "last_login" => "Du kan ikke fjerne din sidste login-metode tilbage — tilknyt en anden eller opret adgangskode først.",
            "not_found" => "Tilknytningen findes ikke længere.",
            "invalid_token" => "Ugyldig session — genindlæs siden og prøv igen.",
            "invalid" => "Ugyldig anmodning.",
            "local_disabled" => "E-mail og adgangskode er slået fra i denne installation.",
            "password_email" => "E-mail skal være en adresse der allerede hører til din Mercantec-konto (samme som ved OAuth-login).",
            "password_invalid" => "Adgangskoder matcher ikke, eller opfylder ikke krav (mindst 8 tegn).",
            "disabled" => "Kontoen er deaktiveret.",
            "dokploy_email" => "Din Auth-konto mangler en e-mail. Tilføj e-mail via login/adgangskode, før Dokploy kan oprettes.",
            "dokploy_disabled" => "Dokploy-integration er ikke aktiveret på serveren.",
            "dokploy_failed" => "Kunne ikke oprette Dokploy-bruger. Prøv igen senere, eller kontakt en admin.",
            _ => null,
        };

        ShowUnlinkedOk = string.Equals(unlinked, "1", StringComparison.Ordinal);
        ShowPasswordSetOk = string.Equals(password_set, "1", StringComparison.Ordinal);
        ShowDokployCreatedOk = string.Equals(dokploy, "created", StringComparison.Ordinal);
        ShowDokployLinkedOk = string.Equals(dokploy, "linked", StringComparison.Ordinal);
        ShowDokployAlreadyOk = string.Equals(dokploy, "already", StringComparison.Ordinal);

        var dokployOpts = dokployOptions.Value;
        DokploySectionVisible = dokployOpts.Enabled && !string.IsNullOrWhiteSpace(dokployOpts.ApiKey);
        DokployUiUrl = dokployOpts.ResolvePublicUiUrl();

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return;

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .Include(u => u.LocalLogin)
            .Include(u => u.LinkedEmails)
            .Include(u => u.DokployLink)
            .FirstAsync(u => u.Id == userId);

        HasPasswordLogin = user.LocalLogin is not null;
        UserEmail = user.Email;
        SuggestedPasswordEmail = user.LocalLogin?.Email
            ?? user.LinkedEmails
                .Where(e => e.Kind == UserEmailKind.Personal)
                .OrderByDescending(e => e.LinkedAt)
                .Select(e => e.NormalizedEmail)
                .FirstOrDefault()
            ?? user.Email;

        if (user.DokployLink is not null)
        {
            DokployIsProvisioned = user.DokployLink.IsProvisioned
                && !string.IsNullOrWhiteSpace(user.DokployLink.DokployUserId);
            DokployUserId = user.DokployLink.DokployUserId;
            DokployLastError = user.DokployLink.LastError;
        }

        ExternalLogins = user.ExternalLogins
            .OrderByDescending(x => x.LinkedAt)
            .Select(x => new ExternalLoginRow(
                x.Id,
                x.Provider,
                ProviderLabelDa(x.Provider),
                x.ProviderEmail,
                x.LinkedAt))
            .ToList();

        OfferedLinkProviders = BuildOfferedProviders();
    }

    private IReadOnlyList<LinkProviderOffer> BuildOfferedProviders()
    {
        var list = new List<LinkProviderOffer>();
        void Add(string key, string name, string hint, bool enabled)
        {
            if (enabled)
            {
                list.Add(new LinkProviderOffer(
                    key,
                    name,
                    hint,
                    $"{Request.PathBase}/account/link/start?provider={Uri.EscapeDataString(key)}&returnUrl={Uri.EscapeDataString(ReturnUrlParam)}"));
            }
        }

        Add(
            "google",
            "Google",
            "Tilknyt din Google-konto — godkend hos Google",
            !string.IsNullOrEmpty(configuration["OAuth:Google:ClientId"]));

        Add(
            "microsoft",
            "Microsoft 365",
            "Tilknyt arbejds- eller Mercantec-konto (Azure AD)",
            MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.WorkSection));

        Add(
            "microsoft-edu",
            "Microsoft — skolemail",
            "Tilknyt skole-/edu-konto (@edu.mercantec.dk)",
            MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.EduSection));

        Add(
            "github",
            "GitHub",
            "Tilknyt GitHub til samme Mercantec-bruger",
            !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]));

        Add(
            "discord",
            "Discord",
            "Tilknyt Discord til samme Mercantec-bruger",
            !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]));

        return list;
    }

    /// <summary>CSS-modifikatorer (<c>auth-provider--*</c>) til udbyderfliser.</summary>
    public static string LinkTileCssModifiers(string providerKey) =>
        providerKey.Trim().ToLowerInvariant() switch
        {
            "google" => "auth-provider--google",
            "microsoft" => "auth-provider--microsoft",
            "microsoft-edu" => "auth-provider--microsoft auth-provider--microsoft-edu",
            "github" => "auth-provider--github",
            "discord" => "auth-provider--discord",
            _ => "",
        };

    /// <summary>Ikonliste for allerede tilknyttede konti.</summary>
    public static string ConnectedRowModifierClass(string provider) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "google" => "linked-conn-row--google",
            "microsoft" => "linked-conn-row--microsoft",
            "microsoft-edu" => "linked-conn-row--microsoft linked-conn-row--microsoft-edu",
            "github" => "linked-conn-row--github",
            "discord" => "linked-conn-row--discord",
            _ => "linked-conn-row--neutral",
        };

    public static string FormatLinkedLocal(DateTime linkedAtUtc)
    {
        var utc = linkedAtUtc.Kind switch
        {
            DateTimeKind.Utc => linkedAtUtc,
            DateTimeKind.Local => linkedAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(linkedAtUtc, DateTimeKind.Utc),
        };

        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, DanishTz);
        return local.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.GetCultureInfo("da-DK"));
    }

    public static string ProviderLabelDa(string provider) =>
        provider.Trim().ToLowerInvariant() switch
        {
            "google" => "Google",
            "microsoft" => "Microsoft",
            "microsoft-edu" => "Microsoft — skolemail",
            "github" => "GitHub",
            "discord" => "Discord",
            _ => provider,
        };
}
