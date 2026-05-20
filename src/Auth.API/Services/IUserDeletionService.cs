namespace Auth.API.Services;

/// <summary>Fjerner en brugerrække og tilhørende login-data (administratorhandling).</summary>
public interface IUserDeletionService
{
    /// <param name="actorUserId">Den bruger der udfører handlingen — må ikke slette sig selv.</param>
    Task<UserDeletionResult> DeleteUserAsync(
        Guid userIdToDelete,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}
