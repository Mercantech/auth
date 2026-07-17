namespace Auth.API.Models.Entities;

public enum DokployAccessRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
}

public class DokployAccessRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DokployAccessRequestStatus Status { get; set; } = DokployAccessRequestStatus.Pending;
    public string? Message { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

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

    public ICollection<DokployAccessRequestProject> Projects { get; set; } = new List<DokployAccessRequestProject>();
}

public class DokployAccessRequestProject
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public DokployAccessRequest Request { get; set; } = null!;
    public string DokployProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
}
