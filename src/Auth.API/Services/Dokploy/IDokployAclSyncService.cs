namespace Auth.API.Services.Dokploy;

/// <summary>Dokploy capability-flags pr. bruger (uden projekter).</summary>
public sealed class DokployCapabilityFlags
{
    public bool CanCreateProjects { get; set; }
    public bool CanCreateServices { get; set; }
    public bool CanDeleteProjects { get; set; }
    public bool CanDeleteServices { get; set; }
    public bool CanAccessToDocker { get; set; }
    public bool CanAccessToTraefikFiles { get; set; }
    public bool CanAccessToAPI { get; set; }
    public bool CanAccessToSSHKeys { get; set; }
    public bool CanAccessToGitProviders { get; set; }
    public bool CanDeleteEnvironments { get; set; }
    public bool CanCreateEnvironments { get; set; }

    public static DokployCapabilityFlags FromLink(Models.Entities.DokployUserLink link) => new()
    {
        CanCreateProjects = link.CanCreateProjects,
        CanCreateServices = link.CanCreateServices,
        CanDeleteProjects = link.CanDeleteProjects,
        CanDeleteServices = link.CanDeleteServices,
        CanAccessToDocker = link.CanAccessToDocker,
        CanAccessToTraefikFiles = link.CanAccessToTraefikFiles,
        CanAccessToAPI = link.CanAccessToAPI,
        CanAccessToSSHKeys = link.CanAccessToSSHKeys,
        CanAccessToGitProviders = link.CanAccessToGitProviders,
        CanDeleteEnvironments = link.CanDeleteEnvironments,
        CanCreateEnvironments = link.CanCreateEnvironments,
    };

    public void ApplyTo(Models.Entities.DokployUserLink link)
    {
        link.CanCreateProjects = CanCreateProjects;
        link.CanCreateServices = CanCreateServices;
        link.CanDeleteProjects = CanDeleteProjects;
        link.CanDeleteServices = CanDeleteServices;
        link.CanAccessToDocker = CanAccessToDocker;
        link.CanAccessToTraefikFiles = CanAccessToTraefikFiles;
        link.CanAccessToAPI = CanAccessToAPI;
        link.CanAccessToSSHKeys = CanAccessToSSHKeys;
        link.CanAccessToGitProviders = CanAccessToGitProviders;
        link.CanDeleteEnvironments = CanDeleteEnvironments;
        link.CanCreateEnvironments = CanCreateEnvironments;
    }
}

public interface IDokployAclSyncService
{
    Task<DokployAclSyncResult> ReconcileAsync(CancellationToken cancellationToken = default);

    Task SavePermissionsAndPushAsync(
        Guid userId,
        IReadOnlyList<(string ProjectId, string? ProjectName)> projects,
        DokployCapabilityFlags capabilities,
        CancellationToken cancellationToken = default);
}

public sealed record DokployAclSyncResult(
    int LinkedUsers,
    int Pushed,
    int Pulled,
    int Errors,
    DateTimeOffset RanAtUtc);
