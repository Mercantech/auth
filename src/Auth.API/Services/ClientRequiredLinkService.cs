using Auth.API.Data;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class ClientRequiredLinkService(AuthDbContext db) : IClientRequiredLinkService
{
    public async Task<bool> MeetsRequirementsAsync(
        Guid userId,
        string? requiredLinkedProvidersRaw,
        CancellationToken cancellationToken = default)
    {
        var required = ClientLoginMethodCatalog.ParseRequiredLinked(requiredLinkedProvidersRaw);
        if (required.Count == 0)
            return true;

        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null)
            return false;

        return ClientRequiredLinkEvaluator.GetMissing(user, required).Count == 0;
    }

    public async Task<IReadOnlyList<ClientLoginMethod>> GetMissingMethodsAsync(
        Guid userId,
        string? requiredLinkedProvidersRaw,
        CancellationToken cancellationToken = default)
    {
        var required = ClientLoginMethodCatalog.ParseRequiredLinked(requiredLinkedProvidersRaw);
        if (required.Count == 0)
            return [];

        var user = await LoadUserAsync(userId, cancellationToken);
        if (user is null)
            return ClientLoginMethodCatalog.ExternalProviders.ToList();

        var missingIds = ClientRequiredLinkEvaluator.GetMissing(user, required);
        return ClientLoginMethodCatalog.ExternalProviders
            .Where(m => missingIds.Contains(m.Id))
            .ToList();
    }

    private Task<User?> LoadUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .Include(u => u.LinkedEmails)
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDisabled, cancellationToken);
}
