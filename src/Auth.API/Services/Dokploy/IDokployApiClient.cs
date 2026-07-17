namespace Auth.API.Services.Dokploy;

public interface IDokployApiClient
{
    Task<IReadOnlyList<DokployUserDto>> ListUsersAsync(CancellationToken cancellationToken = default);
    Task InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default);
    Task CreateUserWithCredentialsAsync(
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default);
    Task<DokployPermissionsDto?> GetPermissionsAsync(
        string dokployUserId,
        CancellationToken cancellationToken = default);
    Task AssignPermissionsAsync(
        DokployAssignPermissionsRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveUserAsync(string dokployUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DokployProjectDto>> ListProjectsForPermissionsAsync(
        CancellationToken cancellationToken = default);
}
