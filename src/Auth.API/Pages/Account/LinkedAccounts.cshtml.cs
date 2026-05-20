using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Pages.Account;

[Authorize]
public class LinkedAccountsModel(
    AuthDbContext db,
    IConfiguration configuration) : PageModel
{
    public IReadOnlyList<ExternalLoginRow> ExternalLogins { get; private set; } = [];
    public IReadOnlyList<LinkProviderChip> OfferedLinkProviders { get; private set; } = [];

    /// <summary>Relativ eller absolut return-sti til brug i link/remove forms.</summary>
    public string ReturnUrlParam { get; private set; } = "/Account/LinkedAccounts";

    public bool HasPasswordLogin { get; private set; }

    public string? BannerError { get; private set; }
    public bool ShowUnlinkedOk { get; private set; }

    public sealed record ExternalLoginRow(Guid Id, string Provider, string ProviderLabel, string? ProviderEmail, DateTime LinkedAtUtc);

    public sealed record LinkProviderChip(string ProviderKey, string Label, string Href);

    private static readonly TimeZoneInfo DanishTz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Romance Standard Time" : "Europe/Copenhagen");

    public async Task OnGetAsync(string? error, string? unlinked)
    {
        ReturnUrlParam = Request.Path.HasValue ? Request.PathBase + Request.Path : "/Account/LinkedAccounts";

        BannerError = error switch
        {
            "link_conflict" => "Denne udbyder-konto tilhører allerede en anden Mercantec-bruger.",
            "last_login" => "Du kan ikke fjerne din sidste login-metode tilbage — tilknyt en anden eller opret adgangskode først.",
            "not_found" => "Tilknytningen findes ikke længere.",
            "invalid_token" => "Ugyldig session — genindlæs siden og prøv igen.",
            "invalid" => "Ugyldig anmodning.",
            _ => null,
        };

        ShowUnlinkedOk = string.Equals(unlinked, "1", StringComparison.Ordinal);

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return;

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .Include(u => u.LocalLogin)
            .FirstAsync(u => u.Id == userId);

        HasPasswordLogin = user.LocalLogin is not null;

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

    private IReadOnlyList<LinkProviderChip> BuildOfferedProviders()
    {
        var chips = new List<LinkProviderChip>();
        void Add(string key, string label, bool enabled)
        {
            if (enabled)
                chips.Add(new LinkProviderChip(key, label,
                    $"{Request.PathBase}/account/link/start?provider={Uri.EscapeDataString(key)}&returnUrl={Uri.EscapeDataString(ReturnUrlParam)}"));
        }

        Add("google", "Google",
            !string.IsNullOrEmpty(configuration["OAuth:Google:ClientId"]));

        Add("microsoft", "Microsoft 365",
            MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.WorkSection));

        Add("microsoft-edu", "Microsoft — skolemail",
            MicrosoftOAuthConfiguration.IsConfigured(configuration, MicrosoftOAuthConfiguration.EduSection));

        Add("github", "GitHub",
            !string.IsNullOrEmpty(configuration["OAuth:GitHub:ClientId"]));

        Add("discord", "Discord",
            !string.IsNullOrEmpty(configuration["OAuth:Discord:ClientId"]));

        return chips;
    }

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
            "github" => "GitHub",
            "discord" => "Discord",
            _ => provider,
        };
}
