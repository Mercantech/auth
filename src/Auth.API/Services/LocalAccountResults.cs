namespace Auth.API.Services;

public enum SetPasswordResult
{
    Created,
    Updated,
    InvalidEmail,
    EmailNotOwnedByUser,
    UserNotFound,
    UserDisabled,
}
