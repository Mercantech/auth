namespace Auth.API.Services;

public enum UserDeletionFailureReason
{
    None,
    NotFound,
    CannotDeleteSelf,
    CannotDeleteLastAdmin,
}

public sealed record UserDeletionResult(bool Success, UserDeletionFailureReason Failure);
