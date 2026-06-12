using Auth.API.Models;
using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface IUserActionTokenService
{
    Task<string> IssueAsync(
        Guid userId,
        UserActionTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<User?> ValidateAndConsumeAsync(
        string plainToken,
        UserActionTokenPurpose purpose,
        CancellationToken cancellationToken = default);
}
