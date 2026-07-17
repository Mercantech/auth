using System.Net.Http.Json;
using System.Text.Json;
using Auth.API.Options;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployApiClient(
    HttpClient http,
    IOptions<DokployOptions> options,
    ILogger<DokployApiClient> logger) : IDokployApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<DokployUserDto>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonAsync("user.all", cancellationToken);
        return ParseUsers(doc.RootElement);
    }

    public async Task InviteMemberAsync(string email, string role, CancellationToken cancellationToken = default)
    {
        await PostJsonAsync(
            "organization.inviteMember",
            new DokployInviteMemberRequest { Email = email, Role = role },
            cancellationToken);
    }

    public async Task CreateUserWithCredentialsAsync(
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        await PostJsonAsync(
            "user.createUserWithCredentials",
            new DokployCreateUserRequest { Email = email, Password = password, Role = role },
            cancellationToken);
    }

    /// <summary>
    /// Henter ACL via <c>user.all</c> (permissions ligger på member-rækken).
    /// <c>user.getPermissions</c> returnerer kun API-nøglens egen session og må ikke bruges til andre brugere.
    /// </summary>
    public async Task<DokployPermissionsDto?> GetPermissionsAsync(
        string dokployUserId,
        CancellationToken cancellationToken = default)
    {
        var users = await ListUsersAsync(cancellationToken);
        var match = users.FirstOrDefault(u =>
            string.Equals(u.ResolvedUserId, dokployUserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.Id, dokployUserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.UserId, dokployUserId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(u.MemberId, dokployUserId, StringComparison.OrdinalIgnoreCase));
        return match?.ToPermissions();
    }

    public Task AssignPermissionsAsync(
        DokployAssignPermissionsRequest request,
        CancellationToken cancellationToken = default)
        => PostJsonAsync("user.assignPermissions", request, cancellationToken);

    public async Task<IReadOnlyList<DokployProjectDto>> ListProjectsForPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var doc = await GetJsonAsync("project.allForPermissions", cancellationToken);
        return ParseProjects(doc.RootElement);
    }

    private async Task<JsonDocument> GetJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await http.GetAsync(relativePath, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Dokploy GET {Path} failed: {Status} {Body}",
                relativePath,
                (int)response.StatusCode,
                Truncate(body));
            throw new DokployApiException(
                $"Dokploy GET {relativePath} returned {(int)response.StatusCode}",
                (int)response.StatusCode,
                body);
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "null" : body);
    }

    private async Task PostJsonAsync<T>(string relativePath, T payload, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        using var response = await http.PostAsJsonAsync(relativePath, payload, JsonOptions, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "Dokploy POST {Path} failed: {Status} {Body}",
                relativePath,
                (int)response.StatusCode,
                Truncate(body));
            throw new DokployApiException(
                $"Dokploy POST {relativePath} returned {(int)response.StatusCode}",
                (int)response.StatusCode,
                body);
        }
    }

    private void EnsureConfigured()
    {
        var opts = options.Value;
        if (!opts.Enabled)
            throw new DokployApiException("Dokploy-integration er deaktiveret.");
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            throw new DokployApiException("Dokploy ApiKey mangler.");
    }

    internal static IReadOnlyList<T> UnwrapArray<T>(JsonElement root)
    {
        var array = FindArray(root);
        if (array is null)
            return [];

        var list = array.Value.Deserialize<List<T>>(JsonOptions);
        return list ?? [];
    }

    /// <summary>
    /// Dokploy <c>user.all</c> returnerer medlemmer med nestet <c>user</c>.
    /// <c>assignPermissions.id</c> skal være <c>user.id</c> / <c>userId</c>, ikke member <c>id</c>.
    /// </summary>
    internal static IReadOnlyList<DokployUserDto> ParseUsers(JsonElement root)
    {
        var array = FindArray(root);
        if (array is null)
            return [];

        var list = new List<DokployUserDto>();
        foreach (var el in array.Value.EnumerateArray())
        {
            if (el.ValueKind is not JsonValueKind.Object)
                continue;

            var memberId = GetString(el, "id");
            var authUserId = GetString(el, "userId")
                ?? GetNestedString(el, "user", "id");
            var email = GetString(el, "email")
                ?? GetNestedString(el, "user", "email")
                ?? GetString(el, "memberEmail");
            var hasNestedUser = TryGetPropertyIgnoreCase(el, "user", out var userEl)
                && userEl.ValueKind is JsonValueKind.Object;

            // assignPermissions matcher member.userId — aldrig member.id når nestet user findes.
            var resolvedId = authUserId ?? (hasNestedUser ? null : memberId);

            if (string.IsNullOrWhiteSpace(resolvedId) && string.IsNullOrWhiteSpace(email))
                continue;

            list.Add(new DokployUserDto
            {
                Id = resolvedId,
                UserId = authUserId ?? resolvedId,
                MemberId = memberId,
                Email = email,
                AccessedProjects = GetStringArray(el, "accessedProjects"),
                CanCreateProjects = GetBool(el, "canCreateProjects"),
                CanCreateServices = GetBool(el, "canCreateServices"),
                CanCreateEnvironments = GetBool(el, "canCreateEnvironments"),
                CanDeleteProjects = GetBool(el, "canDeleteProjects"),
                CanDeleteServices = GetBool(el, "canDeleteServices"),
                CanDeleteEnvironments = GetBool(el, "canDeleteEnvironments"),
                CanAccessToDocker = GetBool(el, "canAccessToDocker"),
                CanAccessToAPI = GetBool(el, "canAccessToAPI"),
                CanAccessToSSHKeys = GetBool(el, "canAccessToSSHKeys"),
                CanAccessToGitProviders = GetBool(el, "canAccessToGitProviders"),
                CanAccessToTraefikFiles = GetBool(el, "canAccessToTraefikFiles"),
            });
        }

        return list;
    }

    private static List<string>? GetStringArray(JsonElement el, string name)
    {
        if (!TryGetPropertyIgnoreCase(el, name, out var prop) || prop.ValueKind is not JsonValueKind.Array)
            return null;
        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }
        }

        return list;
    }

    private static bool? GetBool(JsonElement el, string name)
    {
        if (!TryGetPropertyIgnoreCase(el, name, out var prop))
            return null;
        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    internal static IReadOnlyList<DokployProjectDto> ParseProjects(JsonElement root)
    {
        var array = FindArray(root);
        if (array is null)
            return [];

        var list = new List<DokployProjectDto>();
        foreach (var el in array.Value.EnumerateArray())
        {
            if (el.ValueKind is not JsonValueKind.Object)
                continue;

            var id = GetString(el, "projectId")
                ?? GetString(el, "id")
                ?? GetNestedString(el, "project", "projectId")
                ?? GetNestedString(el, "project", "id");
            var name = GetString(el, "name")
                ?? GetNestedString(el, "project", "name");

            if (string.IsNullOrWhiteSpace(id))
                continue;

            list.Add(new DokployProjectDto
            {
                ProjectId = id,
                Id = id,
                Name = name,
            });
        }

        return list;
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!TryGetPropertyIgnoreCase(el, name, out var prop))
            return null;
        return prop.ValueKind is JsonValueKind.String ? prop.GetString() : null;
    }

    private static string? GetNestedString(JsonElement el, string objectName, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(el, objectName, out var nested) || nested.ValueKind is not JsonValueKind.Object)
            return null;
        return GetString(nested, propertyName);
    }

    internal static JsonElement UnwrapObject(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Object)
        {
            if (TryGetPropertyIgnoreCase(root, "result", out var result)
                && result.ValueKind is JsonValueKind.Object
                && TryGetPropertyIgnoreCase(result, "data", out var data))
            {
                if (data.ValueKind is JsonValueKind.Object
                    && TryGetPropertyIgnoreCase(data, "json", out var json))
                    return json;
                return data;
            }
        }

        return root;
    }

    private static JsonElement? FindArray(JsonElement root)
    {
        if (root.ValueKind is JsonValueKind.Array)
            return root;

        var unwrapped = UnwrapObject(root);
        if (unwrapped.ValueKind is JsonValueKind.Array)
            return unwrapped;

        if (unwrapped.ValueKind is JsonValueKind.Object)
        {
            foreach (var name in new[] { "users", "projects", "data", "items" })
            {
                if (TryGetPropertyIgnoreCase(unwrapped, name, out var prop)
                    && prop.ValueKind is JsonValueKind.Array)
                    return prop;
            }
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string Truncate(string? body)
        => string.IsNullOrEmpty(body) ? string.Empty : body.Length <= 500 ? body : body[..500];
}
