using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface IAccountEmailService
{
    Task SendEmailConfirmationAsync(User user, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(User user, CancellationToken cancellationToken = default);
    Task SendPasswordChangedNoticeAsync(User user, CancellationToken cancellationToken = default);
}
