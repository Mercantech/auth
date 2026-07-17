using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployAccessRequestService(
    AuthDbContext db,
    IDokployProvisionService provision,
    IDokployAclSyncService aclSync,
    IOptions<DokployOptions> options,
    TimeProvider time,
    ILogger<DokployAccessRequestService> logger) : IDokployAccessRequestService
{
    public async Task<IReadOnlyList<DokployAccessRequest>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await db.DokployAccessRequests
            .AsNoTracking()
            .Include(r => r.Projects)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DokployAccessRequest>> ListPendingAsync(
        CancellationToken cancellationToken = default)
        => await db.DokployAccessRequests
            .AsNoTracking()
            .Include(r => r.Projects)
            .Include(r => r.User)
            .Where(r => r.Status == DokployAccessRequestStatus.Pending)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<DokployAccessRequestResult> SubmitAsync(
        Guid userId,
        IReadOnlyList<(string ProjectId, string? ProjectName)> projects,
        DokployCapabilityFlags capabilities,
        string? message,
        string? dokployPasswordIfNeeded,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
            return new DokployAccessRequestResult(false, "Dokploy-integration er deaktiveret.");

        var hasAnyProject = projects.Any(p => !string.IsNullOrWhiteSpace(p.ProjectId));
        var hasAnyCap = HasAnyCapability(capabilities);
        if (!hasAnyProject && !hasAnyCap)
            return new DokployAccessRequestResult(false, "Vælg mindst ét projekt eller én rettighed.");

        var pendingExists = await db.DokployAccessRequests.AnyAsync(
            r => r.UserId == userId && r.Status == DokployAccessRequestStatus.Pending,
            cancellationToken);
        if (pendingExists)
            return new DokployAccessRequestResult(false, "Du har allerede en afventende anmodning. Annullér den først, eller vent på svar.");

        var link = await db.DokployUserLinks.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);
        if (link is not { IsProvisioned: true })
        {
            if (string.IsNullOrWhiteSpace(dokployPasswordIfNeeded) || dokployPasswordIfNeeded.Length < 8)
            {
                return new DokployAccessRequestResult(
                    false,
                    "Du mangler en Dokploy-konto. Angiv en Dokploy-adgangskode (min. 8 tegn) for at oprette den sammen med anmodningen.");
            }

            var provisionResult = await provision.ProvisionAsync(userId, dokployPasswordIfNeeded, cancellationToken);
            if (provisionResult.Status is DokployProvisionStatus.Failed
                or DokployProvisionStatus.Disabled
                or DokployProvisionStatus.MissingEmail
                or DokployProvisionStatus.InvalidPassword)
            {
                return new DokployAccessRequestResult(
                    false,
                    provisionResult.ErrorMessage
                    ?? $"Kunne ikke oprette Dokploy-konto ({provisionResult.Status}).");
            }
        }

        var now = time.GetUtcNow().UtcDateTime;
        var request = new DokployAccessRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = DokployAccessRequestStatus.Pending,
            Message = Truncate(message, 1000),
            CreatedAtUtc = now,
            CanCreateProjects = capabilities.CanCreateProjects,
            CanCreateServices = capabilities.CanCreateServices,
            CanDeleteProjects = capabilities.CanDeleteProjects,
            CanDeleteServices = capabilities.CanDeleteServices,
            CanAccessToDocker = capabilities.CanAccessToDocker,
            CanAccessToTraefikFiles = capabilities.CanAccessToTraefikFiles,
            CanAccessToAPI = capabilities.CanAccessToAPI,
            CanAccessToSSHKeys = capabilities.CanAccessToSSHKeys,
            CanAccessToGitProviders = capabilities.CanAccessToGitProviders,
            CanDeleteEnvironments = capabilities.CanDeleteEnvironments,
            CanCreateEnvironments = capabilities.CanCreateEnvironments,
        };

        foreach (var (projectId, projectName) in projects.DistinctBy(p => p.ProjectId))
        {
            if (string.IsNullOrWhiteSpace(projectId))
                continue;
            request.Projects.Add(new DokployAccessRequestProject
            {
                Id = Guid.NewGuid(),
                RequestId = request.Id,
                DokployProjectId = projectId,
                ProjectName = Truncate(projectName, 256),
            });
        }

        db.DokployAccessRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Dokploy-adgangsanmodning {RequestId} oprettet af {UserId}", request.Id, userId);
        return new DokployAccessRequestResult(true, Request: request);
    }

    public async Task<DokployAccessRequestResult> CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var request = await db.DokployAccessRequests
            .FirstOrDefaultAsync(r => r.Id == requestId && r.UserId == userId, cancellationToken);
        if (request is null)
            return new DokployAccessRequestResult(false, "Anmodning findes ikke.");
        if (request.Status != DokployAccessRequestStatus.Pending)
            return new DokployAccessRequestResult(false, "Kun afventende anmodninger kan annulleres.");

        request.Status = DokployAccessRequestStatus.Cancelled;
        request.ReviewedAtUtc = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return new DokployAccessRequestResult(true, Request: request);
    }

    public async Task<DokployAccessRequestResult> ApproveAsync(
        Guid requestId,
        Guid adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        var request = await db.DokployAccessRequests
            .Include(r => r.Projects)
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
            return new DokployAccessRequestResult(false, "Anmodning findes ikke.");
        if (request.Status != DokployAccessRequestStatus.Pending)
            return new DokployAccessRequestResult(false, "Anmodningen er allerede behandlet.");

        var existingGrants = await db.DokployProjectGrants
            .Where(g => g.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var g in existingGrants)
            merged[g.DokployProjectId] = g.ProjectName;
        foreach (var p in request.Projects)
            merged[p.DokployProjectId] = p.ProjectName ?? merged.GetValueOrDefault(p.DokployProjectId);

        var link = await db.DokployUserLinks.FirstOrDefaultAsync(l => l.UserId == request.UserId, cancellationToken);
        var caps = link is null ? new DokployCapabilityFlags() : DokployCapabilityFlags.FromLink(link);
        OrCapabilities(caps, request);

        try
        {
            await aclSync.SavePermissionsAndPushAsync(
                request.UserId,
                merged.Select(kv => (kv.Key, kv.Value)).ToList(),
                caps,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Godkendelse af Dokploy-anmodning {RequestId} fejlede ved push", requestId);
            return new DokployAccessRequestResult(false, ex.Message);
        }

        request.Status = DokployAccessRequestStatus.Approved;
        request.ReviewedByUserId = adminUserId;
        request.ReviewedAtUtc = time.GetUtcNow().UtcDateTime;
        request.ReviewNote = Truncate(reviewNote, 1000);
        await db.SaveChangesAsync(cancellationToken);
        return new DokployAccessRequestResult(true, Request: request);
    }

    public async Task<DokployAccessRequestResult> RejectAsync(
        Guid requestId,
        Guid adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default)
    {
        var request = await db.DokployAccessRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is null)
            return new DokployAccessRequestResult(false, "Anmodning findes ikke.");
        if (request.Status != DokployAccessRequestStatus.Pending)
            return new DokployAccessRequestResult(false, "Anmodningen er allerede behandlet.");

        request.Status = DokployAccessRequestStatus.Rejected;
        request.ReviewedByUserId = adminUserId;
        request.ReviewedAtUtc = time.GetUtcNow().UtcDateTime;
        request.ReviewNote = Truncate(reviewNote, 1000);
        await db.SaveChangesAsync(cancellationToken);
        return new DokployAccessRequestResult(true, Request: request);
    }

    private static void OrCapabilities(DokployCapabilityFlags target, DokployAccessRequest request)
    {
        target.CanCreateProjects |= request.CanCreateProjects;
        target.CanCreateServices |= request.CanCreateServices;
        target.CanDeleteProjects |= request.CanDeleteProjects;
        target.CanDeleteServices |= request.CanDeleteServices;
        target.CanAccessToDocker |= request.CanAccessToDocker;
        target.CanAccessToTraefikFiles |= request.CanAccessToTraefikFiles;
        target.CanAccessToAPI |= request.CanAccessToAPI;
        target.CanAccessToSSHKeys |= request.CanAccessToSSHKeys;
        target.CanAccessToGitProviders |= request.CanAccessToGitProviders;
        target.CanDeleteEnvironments |= request.CanDeleteEnvironments;
        target.CanCreateEnvironments |= request.CanCreateEnvironments;
    }

    private static bool HasAnyCapability(DokployCapabilityFlags c)
        => c.CanCreateProjects || c.CanCreateServices || c.CanDeleteProjects || c.CanDeleteServices
           || c.CanAccessToDocker || c.CanAccessToTraefikFiles || c.CanAccessToAPI
           || c.CanAccessToSSHKeys || c.CanAccessToGitProviders
           || c.CanDeleteEnvironments || c.CanCreateEnvironments;

    private static string? Truncate(string? value, int max)
        => string.IsNullOrWhiteSpace(value) ? null
            : value.Length <= max ? value.Trim() : value.Trim()[..max];
}
