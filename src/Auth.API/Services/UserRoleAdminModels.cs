namespace Auth.API.Services;

public enum UserRoleAdminFailureReason
{
    None,
    NotFound,
    CannotRemoveLastAdmin,
}

public sealed record UserRoleAdminResult(
    bool Success,
    UserRoleAdminFailureReason Failure,
    IReadOnlyList<string> Roles);
