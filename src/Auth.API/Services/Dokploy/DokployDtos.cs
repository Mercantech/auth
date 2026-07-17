using System.Text.Json.Serialization;

namespace Auth.API.Services.Dokploy;

public sealed class DokployUserDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public sealed class DokployProjectDto
{
    [JsonPropertyName("projectId")]
    public string? ProjectId { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    public string ResolvedId => ProjectId ?? Id ?? string.Empty;
}

public sealed class DokployPermissionsDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("accessedProjects")]
    public List<string>? AccessedProjects { get; set; }
}

public sealed class DokployCreateUserRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }
}

public sealed class DokployInviteMemberRequest
{
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }
}

public sealed class DokployAssignPermissionsRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("accessedProjects")]
    public required IReadOnlyList<string> AccessedProjects { get; init; }

    [JsonPropertyName("accessedEnvironments")]
    public IReadOnlyList<string> AccessedEnvironments { get; init; } = [];

    [JsonPropertyName("accessedServices")]
    public IReadOnlyList<string> AccessedServices { get; init; } = [];

    [JsonPropertyName("accessedGitProviders")]
    public IReadOnlyList<string> AccessedGitProviders { get; init; } = [];

    [JsonPropertyName("accessedServers")]
    public IReadOnlyList<string> AccessedServers { get; init; } = [];

    [JsonPropertyName("canCreateProjects")]
    public bool CanCreateProjects { get; init; }

    [JsonPropertyName("canCreateServices")]
    public bool CanCreateServices { get; init; }

    [JsonPropertyName("canDeleteProjects")]
    public bool CanDeleteProjects { get; init; }

    [JsonPropertyName("canDeleteServices")]
    public bool CanDeleteServices { get; init; }

    [JsonPropertyName("canAccessToDocker")]
    public bool CanAccessToDocker { get; init; }

    [JsonPropertyName("canAccessToTraefikFiles")]
    public bool CanAccessToTraefikFiles { get; init; }

    [JsonPropertyName("canAccessToAPI")]
    public bool CanAccessToAPI { get; init; }

    [JsonPropertyName("canAccessToSSHKeys")]
    public bool CanAccessToSSHKeys { get; init; }

    [JsonPropertyName("canAccessToGitProviders")]
    public bool CanAccessToGitProviders { get; init; }

    [JsonPropertyName("canDeleteEnvironments")]
    public bool CanDeleteEnvironments { get; init; }

    [JsonPropertyName("canCreateEnvironments")]
    public bool CanCreateEnvironments { get; init; }
}
