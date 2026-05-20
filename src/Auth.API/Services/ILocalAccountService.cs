using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface ILocalAccountService
{
    /// <summary>Find bruger til password-login via normaliseret e-mail (UserEmails, profil, LocalLogin).</summary>
    Task<User?> FindUserForPasswordSignInAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Find bruger til implicit password-tilknytning (samme logik som OAuth e-mail-match).</summary>
    Task<User?> FindUserForPasswordLinkByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Opret eller opdater LocalLogin når e-mail tilhører brugeren (OAuth-sync / UserEmails).</summary>
    Task<SetPasswordResult> SetPasswordAsync(
        Guid userId,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    /// <summary>Ny bruger med kun lokalt login + Personal UserEmail + rolle User.</summary>
    Task<User> CreateUserWithPasswordAsync(
        string displayName,
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
