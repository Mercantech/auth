namespace Auth.API.Services;

public enum AccountMergeFailureReason
{
    None,
    SameUser,
    SurvivorNotFound,
    SurvivorDisabled,
    DonorNotFound,
}

public sealed record AccountMergeResult(
    bool Success,
    AccountMergeFailureReason Failure,
    Guid? SurvivorUserId,
    IReadOnlyList<string> Warnings);
