namespace Auth.API.Services;

/// <summary>Administratorhandlinger for Auth-roller pr. bruger.</summary>
public interface IUserRoleAdminService
{
    Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>?> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Erstatter brugerens roller. Ukendte rollenavne oprettes. Kan ikke fjerne Admin fra sidste Admin.</summary>
    Task<UserRoleAdminResult> SetUserRolesAsync(
        Guid userId,
        IEnumerable<string>? roles,
        CancellationToken cancellationToken = default);
}
