using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public sealed class AccountEmailService(
    IEmailService emailService,
    IUserActionTokenService tokenService,
    EmailTemplateRenderer templates,
    IOptions<EmailOptions> emailOptions) : IAccountEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendEmailConfirmationAsync(User user, CancellationToken cancellationToken = default)
    {
        var token = await tokenService.IssueAsync(
            user.Id,
            UserActionTokenPurpose.EmailConfirmation,
            TimeSpan.FromHours(_options.EmailConfirmExpiryHours),
            cancellationToken);

        var actionUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/account/confirm-email?token={Uri.EscapeDataString(token)}";
        var html = await templates.RenderAsync("confirm-email.html", new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName,
            ["actionUrl"] = actionUrl,
            ["expiryHours"] = _options.EmailConfirmExpiryHours.ToString(),
            ["fromName"] = _options.FromName,
        });

        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = "Bekræft din e-mail — Mercantec Auth",
            HtmlBody = html,
        }, cancellationToken);
    }

    public async Task SendPasswordResetAsync(User user, CancellationToken cancellationToken = default)
    {
        var token = await tokenService.IssueAsync(
            user.Id,
            UserActionTokenPurpose.PasswordReset,
            TimeSpan.FromMinutes(_options.PasswordResetExpiryMinutes),
            cancellationToken);

        var actionUrl = $"{_options.PublicBaseUrl.TrimEnd('/')}/Account/ResetPassword?token={Uri.EscapeDataString(token)}";
        var html = await templates.RenderAsync("reset-password.html", new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName,
            ["actionUrl"] = actionUrl,
            ["expiryMinutes"] = _options.PasswordResetExpiryMinutes.ToString(),
            ["fromName"] = _options.FromName,
        });

        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = "Nulstil din adgangskode — Mercantec Auth",
            HtmlBody = html,
        }, cancellationToken);
    }

    public async Task SendPasswordChangedNoticeAsync(User user, CancellationToken cancellationToken = default)
    {
        var html = await templates.RenderAsync("password-changed.html", new Dictionary<string, string>
        {
            ["displayName"] = user.DisplayName,
            ["fromName"] = _options.FromName,
            ["supportUrl"] = _options.PublicBaseUrl.TrimEnd('/'),
        });

        await emailService.SendAsync(new EmailMessage
        {
            ToAddress = RequireEmail(user),
            ToName = user.DisplayName,
            Subject = "Din adgangskode er ændret — Mercantec Auth",
            HtmlBody = html,
        }, cancellationToken);
    }

    private static string RequireEmail(User user) =>
        string.IsNullOrWhiteSpace(user.Email)
            ? throw new InvalidOperationException($"Bruger {user.Id} har ingen e-mail.")
            : user.Email;
}
