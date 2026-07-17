namespace Auth.API.Options;

public class DokployOptions
{
    public const string SectionName = "Dokploy";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://deploy.mags.dk/api";
    public string ApiKey { get; set; } = string.Empty;
    public string MemberRole { get; set; } = "member";
    public int AclSyncIntervalMinutes { get; set; } = 15;

    /// <summary>Offentlig Dokploy UI-URL (til “Åbn deploy”-link). Tom = udled fra BaseUrl.</summary>
    public string? PublicUiUrl { get; set; }

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

    public string ResolvePublicUiUrl()
    {
        if (!string.IsNullOrWhiteSpace(PublicUiUrl))
            return PublicUiUrl.TrimEnd('/');

        var baseUrl = (BaseUrl ?? "").TrimEnd('/');
        if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            return baseUrl[..^4];

        return string.IsNullOrWhiteSpace(baseUrl) ? "https://deploy.mags.dk" : baseUrl;
    }
}
