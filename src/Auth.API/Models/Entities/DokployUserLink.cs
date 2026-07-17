namespace Auth.API.Models.Entities;

public class DokployUserLink
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? DokployUserId { get; set; }
    public string LinkedEmail { get; set; } = string.Empty;
    public DateTime? ProvisionedAtUtc { get; set; }
    public string? LastError { get; set; }
    public bool IsProvisioned { get; set; }
    public bool AclDirty { get; set; }
    public DateTime? AclSyncedAtUtc { get; set; }

    // Dokploy capability flags (assignPermissions)
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
}
