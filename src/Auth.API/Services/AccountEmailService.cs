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

    public async Task SendPasswordChangedNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default)
    {
        var palette = await EmailThemeCatalog.ResolveForClientAsync(db, emailOptions, clientId, cancellationToken);
        var values = new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName,
            ["supportUrl"] = _options.PublicBaseUrl.TrimEnd('/'),
            ["preheader"] = "Din adgangskode er blevet ændret.",
        };
        var html = await templates.RenderThemedAsync("password-changed.html", palette, values);
        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = $"Din adgangskode er ændret — {palette.BrandTitle}",
            HtmlBody = html,
        }, cancellationToken);
    }

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
