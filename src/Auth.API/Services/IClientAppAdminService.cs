using Auth.API.Models.Entities;

namespace Auth.API.Services;

public interface IClientAppAdminService
{
    Task<IReadOnlyList<ClientAppAdminListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<ClientAppAdminDetail?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> CreateAsync(
        CreateClientAppRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> UpdateAsync(
        string clientId,
        UpdateClientAppRequest request,
        CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> AddRedirectUriAsync(
        string clientId,
        string uri,
        CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> RemoveRedirectUriAsync(
        string clientId,
        Guid redirectUriId,
        CancellationToken cancellationToken = default);

    Task<ClientAppAdminMutationResult> RotateSecretAsync(
        string clientId,
        CancellationToken cancellationToken = default);
}

public sealed record CreateClientAppRequest(
    string Name,
    string ClientId,
    bool IsActive = true,
    bool IsPublic = true,
    bool RequirePkce = true,
    string? AllowedScopes = null);

public sealed record UpdateClientAppRequest(
    string? Name = null,
    string? NewClientId = null,
    bool? IsActive = null,
    bool? IsPublic = null,
    bool? RequirePkce = null,
    string? AllowedScopes = null,
    int? AccessTokenExpiryMinutes = null,
    bool ClearAccessTokenExpiryMinutes = false,
    int? RefreshTokenExpiryDays = null,
    bool ClearRefreshTokenExpiryDays = false,
    string? LoginThemeId = null,
    bool ClearLoginThemeId = false,
    IReadOnlyList<string>? AllowedLoginMethods = null,
    bool UseAllLoginMethods = false,
    IReadOnlyList<string>? RequiredLinkedProviders = null,
    bool ClearRequiredLinkedProviders = false);

public sealed record ClientAppAdminListItem(
    Guid Id,
    string ClientId,
    string Name,
    bool IsActive,
    bool IsPublic,
    bool RequirePkce,
    int RedirectUriCount,
    DateTime CreatedAt);

public sealed record ClientAppRedirectUriDto(Guid Id, string Uri);

public sealed record ClientAppAdminDetail(
    Guid Id,
    string ClientId,
    string Name,
    bool IsActive,
    bool IsPublic,
    bool RequirePkce,
    string? AllowedScopes,
    int? AccessTokenExpiryMinutes,
    int? RefreshTokenExpiryDays,
    string? LoginThemeId,
    string? AllowedLoginMethods,
    string? RequiredLinkedProviders,
    bool HasClientSecret,
    DateTime CreatedAt,
    IReadOnlyList<ClientAppRedirectUriDto> RedirectUris);

public enum ClientAppAdminFailure
{
    None,
    NotFound,
    Validation,
    Conflict,
}

public sealed record ClientAppAdminMutationResult(
    bool Success,
    ClientAppAdminFailure Failure,
    string? Error = null,
    ClientAppAdminDetail? Client = null,
    string? PlaintextSecret = null)
{
    public static ClientAppAdminMutationResult Ok(ClientAppAdminDetail client, string? plaintextSecret = null) =>
        new(true, ClientAppAdminFailure.None, null, client, plaintextSecret);

    public static ClientAppAdminMutationResult Fail(ClientAppAdminFailure failure, string error) =>
        new(false, failure, error);

    public static ClientAppAdminMutationResult Deleted() =>
        new(true, ClientAppAdminFailure.None);
}
