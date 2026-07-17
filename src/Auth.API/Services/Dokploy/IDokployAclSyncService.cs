namespace Auth.API.Services.Dokploy;

public interface IDokployAclSyncService
{
    Task<DokployAclSyncResult> ReconcileAsync(CancellationToken cancellationToken = default);

    Task SaveGrantsAndPushAsync(
        Guid userId,
        IReadOnlyList<(string ProjectId, string? ProjectName)> projects,
        CancellationToken cancellationToken = default);
}

public sealed record DokployAclSyncResult(
    int LinkedUsers,
    int Pushed,
    int Pulled,
    int Errors,
    DateTimeOffset RanAtUtc);
