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
}
