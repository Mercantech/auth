using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface IAccountEmailService
{
    Task SendEmailConfirmationAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasswordResetAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasswordChangedNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendTotpEnabledNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendTotpDisabledNoticeAsync(User user, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasskeyAddedNoticeAsync(User user, string passkeyName, string? clientId = null, CancellationToken cancellationToken = default);
    Task SendPasskeyRemovedNoticeAsync(User user, string passkeyName, string? clientId = null, CancellationToken cancellationToken = default);
}
