using Auth.API.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Auth.API.Services;

public sealed class SmtpEmailService(IOptions<EmailOptions> emailOptions, ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost))
            throw new InvalidOperationException("Email:SmtpHost er ikke konfigureret.");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToAddress, message.ToAddress));
        mime.Subject = message.Subject;

        if (!string.IsNullOrWhiteSpace(_options.ReplyToAddress))
            mime.ReplyTo.Add(MailboxAddress.Parse(_options.ReplyToAddress));

        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPassword ?? string.Empty, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        logger.LogInformation("E-mail sendt til {To}: {Subject}", message.ToAddress, message.Subject);
    }
}
