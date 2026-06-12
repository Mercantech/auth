namespace Auth.API.Services;

public sealed class NoOpEmailService(ILogger<NoOpEmailService> logger) : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "E-mail deaktiveret — ville sende til {To}: {Subject}",
            message.ToAddress,
            message.Subject);
        return Task.CompletedTask;
    }
}
