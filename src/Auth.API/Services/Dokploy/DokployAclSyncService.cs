using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployAclSyncService(
    AuthDbContext db,
    IDokployApiClient api,
    IOptions<DokployOptions> options,
    TimeProvider time,
    ILogger<DokployAclSyncService> logger) : IDokployAclSyncService
{
    public async Task<DokployAclSyncResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var ranAt = time.GetUtcNow();
        if (!opts.Enabled)
            return new DokployAclSyncResult(0, 0, 0, 0, ranAt);

        var pushed = 0;
        var pulled = 0;
        var errors = 0;

        await LinkExistingDokployUsersByEmailAsync(cancellationToken);

        var links = await db.DokployUserLinks
            .Include(l => l.User)
            .Where(l => l.IsProvisioned || l.DokployUserId != null)
            .ToListAsync(cancellationToken);

        var projectNames = await LoadProjectNameMapAsync(cancellationToken);

        foreach (var link in links)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(link.DokployUserId))
                {
                    await ResolveDokployUserIdAsync(link, cancellationToken);
                    if (string.IsNullOrWhiteSpace(link.DokployUserId))
                        continue;
                }

                if (link.AclDirty)
                {
                    await PushPermissionsAsync(link, cancellationToken);
                    pushed++;
                }
                else
                {
                    await PullPermissionsAsync(link, projectNames, cancellationToken);
                    pulled++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "Dokploy ACL-sync fejlede for Auth-bruger {UserId}", link.UserId);
                link.LastError = Truncate(ex.Message);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return new DokployAclSyncResult(links.Count, pushed, pulled, errors, ranAt);
    }

    public async Task SaveGrantsAndPushAsync(
        Guid userId,
        IReadOnlyList<(string ProjectId, string? ProjectName)> projects,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled)
            throw new InvalidOperationException("Dokploy-integration er deaktiveret.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new InvalidOperationException("Bruger findes ikke.");

        var norm = EmailNormalizer.Normalize(user.Email);
        var link = await db.DokployUserLinks.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);
        if (link is null)
        {
            if (norm is null)
                throw new InvalidOperationException("Brugeren mangler e-mail til Dokploy-link.");

            link = new DokployUserLink
            {
                UserId = userId,
                LinkedEmail = norm,
            };
            db.DokployUserLinks.Add(link);
            await db.SaveChangesAsync(cancellationToken);
            await ResolveDokployUserIdAsync(link, cancellationToken);
        }

        var existing = await db.DokployProjectGrants
            .Where(g => g.UserId == userId)
            .ToListAsync(cancellationToken);
        db.DokployProjectGrants.RemoveRange(existing);

        var now = time.GetUtcNow().UtcDateTime;
        foreach (var (projectId, projectName) in projects.DistinctBy(p => p.ProjectId))
        {
            if (string.IsNullOrWhiteSpace(projectId))
                continue;
            db.DokployProjectGrants.Add(new DokployProjectGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                DokployProjectId = projectId,
                ProjectName = projectName,
                GrantedAtUtc = now,
            });
        }

        link.AclDirty = true;
        link.LastError = null;
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(link.DokployUserId))
            await PushPermissionsAsync(link, cancellationToken);
    }

    private async Task LinkExistingDokployUsersByEmailAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DokployUserDto> dokployUsers;
        try
        {
            dokployUsers = await api.ListUsersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kunne ikke hente Dokploy-brugere til e-mail-match");
            return;
        }

        var byEmail = dokployUsers
            .Select(u => (Norm: EmailNormalizer.Normalize(u.Email), Id: u.Id ?? u.UserId))
            .Where(x => x.Norm is not null && !string.IsNullOrWhiteSpace(x.Id))
            .GroupBy(x => x.Norm!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Id!, StringComparer.Ordinal);

        if (byEmail.Count == 0)
            return;

        var authUsers = await db.Users
            .Where(u => u.Email != null && !u.IsDisabled)
            .Select(u => new { u.Id, u.Email })
            .ToListAsync(cancellationToken);

        var existingLinks = await db.DokployUserLinks
            .Select(l => l.UserId)
            .ToListAsync(cancellationToken);
        var linked = existingLinks.ToHashSet();

        var now = time.GetUtcNow().UtcDateTime;
        foreach (var u in authUsers)
        {
            if (linked.Contains(u.Id))
                continue;
            var norm = EmailNormalizer.Normalize(u.Email);
            if (norm is null || !byEmail.TryGetValue(norm, out var dokployId))
                continue;

            db.DokployUserLinks.Add(new DokployUserLink
            {
                UserId = u.Id,
                DokployUserId = dokployId,
                LinkedEmail = norm,
                IsProvisioned = true,
                ProvisionedAtUtc = now,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ResolveDokployUserIdAsync(DokployUserLink link, CancellationToken cancellationToken)
    {
        var users = await api.ListUsersAsync(cancellationToken);
        var match = users.FirstOrDefault(u =>
            EmailNormalizer.Normalize(u.Email) == link.LinkedEmail
            || EmailNormalizer.Normalize(u.Email) == EmailNormalizer.Normalize(link.User?.Email));
        var id = match?.Id ?? match?.UserId;
        if (string.IsNullOrWhiteSpace(id))
            return;

        link.DokployUserId = id;
        link.IsProvisioned = true;
        link.ProvisionedAtUtc ??= time.GetUtcNow().UtcDateTime;
        link.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PushPermissionsAsync(DokployUserLink link, CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var projectIds = await db.DokployProjectGrants
            .Where(g => g.UserId == link.UserId)
            .Select(g => g.DokployProjectId)
            .ToListAsync(cancellationToken);

        await api.AssignPermissionsAsync(
            new DokployAssignPermissionsRequest
            {
                Id = link.DokployUserId!,
                AccessedProjects = projectIds,
                CanCreateProjects = opts.CanCreateProjects,
                CanCreateServices = opts.CanCreateServices,
                CanDeleteProjects = opts.CanDeleteProjects,
                CanDeleteServices = opts.CanDeleteServices,
                CanAccessToDocker = opts.CanAccessToDocker,
                CanAccessToTraefikFiles = opts.CanAccessToTraefikFiles,
                CanAccessToAPI = opts.CanAccessToAPI,
                CanAccessToSSHKeys = opts.CanAccessToSSHKeys,
                CanAccessToGitProviders = opts.CanAccessToGitProviders,
                CanDeleteEnvironments = opts.CanDeleteEnvironments,
                CanCreateEnvironments = opts.CanCreateEnvironments,
            },
            cancellationToken);

        link.AclDirty = false;
        link.AclSyncedAtUtc = time.GetUtcNow().UtcDateTime;
        link.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task PullPermissionsAsync(
        DokployUserLink link,
        IReadOnlyDictionary<string, string?> projectNames,
        CancellationToken cancellationToken)
    {
        var perms = await api.GetPermissionsAsync(link.DokployUserId!, cancellationToken);
        var remote = perms?.AccessedProjects ?? [];

        var existing = await db.DokployProjectGrants
            .Where(g => g.UserId == link.UserId)
            .ToListAsync(cancellationToken);
        db.DokployProjectGrants.RemoveRange(existing);

        var now = time.GetUtcNow().UtcDateTime;
        foreach (var projectId in remote.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(projectId))
                continue;
            projectNames.TryGetValue(projectId, out var name);
            db.DokployProjectGrants.Add(new DokployProjectGrant
            {
                Id = Guid.NewGuid(),
                UserId = link.UserId,
                DokployProjectId = projectId,
                ProjectName = name,
                GrantedAtUtc = now,
            });
        }

        link.AclDirty = false;
        link.AclSyncedAtUtc = now;
        link.LastError = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, string?>> LoadProjectNameMapAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var projects = await api.ListProjectsForPermissionsAsync(cancellationToken);
            return projects
                .Where(p => !string.IsNullOrWhiteSpace(p.ResolvedId))
                .GroupBy(p => p.ResolvedId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kunne ikke hente Dokploy-projekter til navne-cache");
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }
    }

    private static string Truncate(string message)
        => message.Length <= 1000 ? message : message[..1000];
}
