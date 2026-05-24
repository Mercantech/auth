namespace Auth.API.Services;

public interface IClientRequiredLinkService
{
    Task<bool> MeetsRequirementsAsync(Guid userId, string? requiredLinkedProvidersRaw, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientLoginMethod>> GetMissingMethodsAsync(
        Guid userId,
        string? requiredLinkedProvidersRaw,
        CancellationToken cancellationToken = default);
}
