namespace Auth.API.Options;

public class DokployOptions
{
    public const string SectionName = "Dokploy";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://deploy.mags.dk/api";
    public string ApiKey { get; set; } = string.Empty;
    public string MemberRole { get; set; } = "member";
    public int AclSyncIntervalMinutes { get; set; } = 15;

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
