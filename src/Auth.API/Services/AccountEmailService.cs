using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public sealed class AccountEmailService(
    AuthDbContext db,
    IEmailService emailService,
    IUserActionTokenService tokenService,
    EmailTemplateRenderer templates,
    IOptions<EmailOptions> emailOptions) : IAccountEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

    public Task SendEmailConfirmationAsync(User user, string? clientId = null, CancellationToken cancellationToken = default) =>
        SendTemplatedAsync(
            user,
            clientId,
            "confirm-email.html",
            $"Bekræft din e-mail",
            "Bekræft din e-mail for at aktivere din konto.",
            async token =>
            {
                var actionUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/account/confirm-email?token={Uri.EscapeDataString(token)}";
                return new Dictionary<string, string>
                {
                    ["displayName"] = user.DisplayName,
                    ["actionUrl"] = actionUrl,
                    ["expiryHours"] = _options.EmailConfirmExpiryHours.ToString(),
                };
            },
            UserActionTokenPurpose.EmailConfirmation,
            TimeSpan.FromHours(_options.EmailConfirmExpiryHours),
            cancellationToken);

    public Task SendPasswordResetAsync(User user, string? clientId = null, CancellationToken cancellationToken = default) =>
        SendTemplatedAsync(
            user,
            clientId,
            "reset-password.html",
            "Nulstil din adgangskode",
            "Du har anmodet om at nulstille din adgangskode.",
            async token =>
            {
                var actionUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/Account/ResetPassword?token={Uri.EscapeDataString(token)}";
                return new Dictionary<string, string>
                {
                    ["displayName"] = user.DisplayName,
                    ["actionUrl"] = actionUrl,
                    ["expiryMinutes"] = _options.PasswordResetExpiryMinutes.ToString(),
                };
            },
            UserActionTokenPurpose.PasswordReset,
            TimeSpan.FromMinutes(_options.PasswordResetExpiryMinutes),
            cancellationToken);

    public async Task SendPasswordChangedNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default) =>
        await SendSecurityNoticeAsync(
            user,
            clientId,
            "Din adgangskode er ændret",
            "Din adgangskode er blevet ændret.",
            "Adgangskode opdateret",
            $"din adgangskode til {{brandTitle}} er blevet ændret.",
            cancellationToken: cancellationToken);

    public Task SendTotpEnabledNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default) =>
        SendSecurityNoticeAsync(
            user,
            clientId,
            "To-faktor aktiveret",
            "Authenticator-app (TOTP) er aktiveret på din konto.",
            "To-faktor aktiveret",
            "to-faktor-godkendelse via authenticator-app er nu aktiveret på din konto.",
            cancellationToken: cancellationToken);

    public Task SendTotpDisabledNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default) =>
        SendSecurityNoticeAsync(
            user,
            clientId,
            "To-faktor deaktiveret",
            "Authenticator-app (TOTP) er deaktiveret på din konto.",
            "To-faktor deaktiveret",
            "to-faktor-godkendelse via authenticator-app er blevet deaktiveret på din konto.",
            cancellationToken: cancellationToken);

    public Task SendPasskeyAddedNoticeAsync(
        User user,
        string passkeyName,
        string? clientId = null,
        CancellationToken cancellationToken = default) =>
        SendSecurityNoticeAsync(
            user,
            clientId,
            "Ny passkey tilføjet",
            $"En ny passkey ({passkeyName}) er registreret på din konto.",
            "Passkey tilføjet",
            "en ny passkey er blevet registreret på din konto.",
            DetailParagraph("Passkey", passkeyName),
            cancellationToken);

    public Task SendPasskeyRemovedNoticeAsync(
        User user,
        string passkeyName,
        string? clientId = null,
        CancellationToken cancellationToken = default) =>
        SendSecurityNoticeAsync(
            user,
            clientId,
            "Passkey fjernet",
            $"Passkey «{passkeyName}» er fjernet fra din konto.",
            "Passkey fjernet",
            "en passkey er blevet fjernet fra din konto.",
            DetailParagraph("Passkey", passkeyName),
            cancellationToken);

    private async Task SendSecurityNoticeAsync(
        User user,
        string? clientId,
        string subjectCore,
        string preheader,
        string eventTitle,
        string eventBody,
        string? eventDetailBlock = null,
        CancellationToken cancellationToken = default)
    {
        var palette = await EmailThemeCatalog.ResolveForClientAsync(db, emailOptions, clientId, cancellationToken);
        var body = eventBody.Replace("{brandTitle}", palette.BrandTitle, StringComparison.Ordinal);
        var values = new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName,
            ["eventTitle"] = eventTitle,
            ["eventBody"] = body,
            ["eventDetailBlock"] = eventDetailBlock ?? string.Empty,
            ["supportUrl"] = _options.PublicBaseUrl.TrimEnd('/'),
            ["preheader"] = preheader,
        };
        var html = await templates.RenderThemedAsync("security-notice.html", palette, values);
        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = $"{subjectCore} — {palette.BrandTitle}",
            HtmlBody = html,
        }, cancellationToken);
    }

    private static string DetailParagraph(string label, string value) =>
        $"""<p style="margin:0 0 16px;font-size:14px;line-height:1.55;color:#524a42;"><strong>{label}:</strong> {System.Net.WebUtility.HtmlEncode(value)}</p>""";

    private async Task SendTemplatedAsync(
        User user,
        string? clientId,
        string templateFile,
        string subjectCore,
        string preheader,
        Func<string, Task<Dictionary<string, string>>> buildValues,
        UserActionTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.IssueAsync(user.Id, purpose, lifetime, cancellationToken);
        var palette = await EmailThemeCatalog.ResolveForClientAsync(db, emailOptions, clientId, cancellationToken);
        var values = await buildValues(token);
        values["preheader"] = preheader;
        var html = await templates.RenderThemedAsync(templateFile, palette, values);

        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = $"{subjectCore} — {palette.BrandTitle}",
            HtmlBody = html,
        }, cancellationToken);
    }

    private static string RequireEmail(User user) =>
        string.IsNullOrWhiteSpace(user.Email)
            ? throw new InvalidOperationException($"Bruger {user.Id} har ingen e-mail.")
            : user.Email;
}
