using System.Text.Json.Serialization;

namespace Auth.API.Services.Dokploy;

/// <summary>
/// Dokploy <c>user.all</c> returnerer organisation-medlemmer.
/// <see cref="Id"/> / <see cref="UserId"/> er Better Auth-bruger-id (bruges i <c>assignPermissions</c>).
/// <see cref="MemberId"/> er member-række-id og må ikke sendes til assignPermissions.
/// </summary>
public sealed class DokployUserDto
{
    /// <summary>Better Auth user id — det id assignPermissions forventer.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    /// <summary>Organisation member row id (ikke til assignPermissions).</summary>
    public string? MemberId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("accessedProjects")]
    public List<string>? AccessedProjects { get; set; }

    [JsonPropertyName("canCreateProjects")]
    public bool? CanCreateProjects { get; set; }

    [JsonPropertyName("canCreateServices")]
    public bool? CanCreateServices { get; set; }

    [JsonPropertyName("canCreateEnvironments")]
    public bool? CanCreateEnvironments { get; set; }

    [JsonPropertyName("canDeleteProjects")]
    public bool? CanDeleteProjects { get; set; }

    [JsonPropertyName("canDeleteServices")]
    public bool? CanDeleteServices { get; set; }

    [JsonPropertyName("canDeleteEnvironments")]
    public bool? CanDeleteEnvironments { get; set; }

    [JsonPropertyName("canAccessToDocker")]
    public bool? CanAccessToDocker { get; set; }

    [JsonPropertyName("canAccessToAPI")]
    public bool? CanAccessToAPI { get; set; }

    [JsonPropertyName("canAccessToSSHKeys")]
    public bool? CanAccessToSSHKeys { get; set; }

    [JsonPropertyName("canAccessToGitProviders")]
    public bool? CanAccessToGitProviders { get; set; }

    [JsonPropertyName("canAccessToTraefikFiles")]
    public bool? CanAccessToTraefikFiles { get; set; }

    public string? ResolvedUserId =>
        !string.IsNullOrWhiteSpace(UserId) ? UserId
        : !string.IsNullOrWhiteSpace(Id) ? Id
        : null;

    public DokployPermissionsDto ToPermissions() => new()
    {
        Id = ResolvedUserId,
        AccessedProjects = AccessedProjects,
        CanCreateProjects = CanCreateProjects,
        CanCreateServices = CanCreateServices,
        CanCreateEnvironments = CanCreateEnvironments,
        CanDeleteProjects = CanDeleteProjects,
        CanDeleteServices = CanDeleteServices,
        CanDeleteEnvironments = CanDeleteEnvironments,
        CanAccessToDocker = CanAccessToDocker,
        CanAccessToAPI = CanAccessToAPI,
        CanAccessToSSHKeys = CanAccessToSSHKeys,
        CanAccessToGitProviders = CanAccessToGitProviders,
        CanAccessToTraefikFiles = CanAccessToTraefikFiles,
    };
}

public sealed class DokployProjectDto
{
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public List<DokployEnvironmentDto> Environments { get; set; } = [];

    public string ResolvedId => ProjectId ?? Id ?? string.Empty;
}

public sealed class DokployEnvironmentDto
{
    public string? EnvironmentId { get; set; }
    public string? Name { get; set; }
    public List<string> ServiceIds { get; set; } = [];
}

public sealed class DokployPermissionsDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("accessedProjects")]
    public List<string>? AccessedProjects { get; set; }

    [JsonPropertyName("accessedEnvironments")]
    public List<string>? AccessedEnvironments { get; set; }

    [JsonPropertyName("accessedServices")]
    public List<string>? AccessedServices { get; set; }

    [JsonPropertyName("accessedGitProviders")]
    public List<string>? AccessedGitProviders { get; set; }

    [JsonPropertyName("accessedServers")]
    public List<string>? AccessedServers { get; set; }

    [JsonPropertyName("canCreateProjects")]
    public bool? CanCreateProjects { get; set; }

    [JsonPropertyName("canCreateServices")]
    public bool? CanCreateServices { get; set; }

    [JsonPropertyName("canCreateEnvironments")]
    public bool? CanCreateEnvironments { get; set; }

    [JsonPropertyName("canDeleteServices")]
    public bool? CanDeleteServices { get; set; }

    [JsonPropertyName("canDeleteProjects")]
    public bool? CanDeleteProjects { get; set; }

    [JsonPropertyName("canDeleteEnvironments")]
    public bool? CanDeleteEnvironments { get; set; }

    [JsonPropertyName("canAccessToDocker")]
    public bool? CanAccessToDocker { get; set; }

    [JsonPropertyName("canAccessToAPI")]
    public bool? CanAccessToAPI { get; set; }

    [JsonPropertyName("canAccessToSSHKeys")]
    public bool? CanAccessToSSHKeys { get; set; }

    [JsonPropertyName("canAccessToGitProviders")]
    public bool? CanAccessToGitProviders { get; set; }

    [JsonPropertyName("canAccessToTraefikFiles")]
    public bool? CanAccessToTraefikFiles { get; set; }
}

public sealed class DokployAssignPermissionsRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("accessedProjects")]
    public List<string> AccessedProjects { get; set; } = [];

    [JsonPropertyName("accessedEnvironments")]
    public List<string> AccessedEnvironments { get; set; } = [];

    [JsonPropertyName("accessedServices")]
    public List<string> AccessedServices { get; set; } = [];

    [JsonPropertyName("accessedGitProviders")]
    public List<string> AccessedGitProviders { get; set; } = [];

    [JsonPropertyName("accessedServers")]
    public List<string> AccessedServers { get; set; } = [];

    [JsonPropertyName("canCreateProjects")]
    public bool CanCreateProjects { get; set; }

    [JsonPropertyName("canCreateServices")]
    public bool CanCreateServices { get; set; }

    [JsonPropertyName("canCreateEnvironments")]
    public bool CanCreateEnvironments { get; set; }

    [JsonPropertyName("canDeleteServices")]
    public bool CanDeleteServices { get; set; }

    [JsonPropertyName("canDeleteProjects")]
    public bool CanDeleteProjects { get; set; }

    [JsonPropertyName("canDeleteEnvironments")]
    public bool CanDeleteEnvironments { get; set; }

    [JsonPropertyName("canAccessToDocker")]
    public bool CanAccessToDocker { get; set; }

    [JsonPropertyName("canAccessToAPI")]
    public bool CanAccessToAPI { get; set; }

    [JsonPropertyName("canAccessToSSHKeys")]
    public bool CanAccessToSSHKeys { get; set; }

    [JsonPropertyName("canAccessToGitProviders")]
    public bool CanAccessToGitProviders { get; set; }

    [JsonPropertyName("canAccessToTraefikFiles")]
    public bool CanAccessToTraefikFiles { get; set; }
}

public sealed class DokployCreateUserRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = "member";
}

public sealed class DokployInviteMemberRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("role")]
    public required string Role { get; set; }
}

public sealed class DokployRemoveUserRequest
{
    [JsonPropertyName("userId")]
    public required string UserId { get; set; }
}
