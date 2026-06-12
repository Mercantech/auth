using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface IAccountEmailService
{
    Task SendEmailConfirmationAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasswordChangedNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
}
