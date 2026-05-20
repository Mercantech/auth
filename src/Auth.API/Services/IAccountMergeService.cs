namespace Auth.API.Services;

/// <summary>Administrativ sammenlægning af to bruger-rækker til én canonisk bruger (<paramref name="survivorUserId"/>).</summary>
public interface IAccountMergeService
{
    Task<AccountMergeResult> MergeUsersAsync(
        Guid survivorUserId,
        Guid donorUserId,
        CancellationToken cancellationToken = default);
}
