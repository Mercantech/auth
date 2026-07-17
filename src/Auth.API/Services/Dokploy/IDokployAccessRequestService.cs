using Auth.API.Models.Entities;

namespace Auth.API.Services.Dokploy;

public interface IDokployAccessRequestService
{
    Task<IReadOnlyList<DokployAccessRequest>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DokployAccessRequest>> ListPendingAsync(
        CancellationToken cancellationToken = default);

    Task<DokployAccessRequestResult> SubmitAsync(
        Guid userId,
        IReadOnlyList<(string ProjectId, string? ProjectName)> projects,
        DokployCapabilityFlags capabilities,
        string? message,
        string? dokployPasswordIfNeeded,
        CancellationToken cancellationToken = default);

    Task<DokployAccessRequestResult> CancelAsync(
        Guid userId,
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<DokployAccessRequestResult> ApproveAsync(
        Guid requestId,
        Guid adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default);

    Task<DokployAccessRequestResult> RejectAsync(
        Guid requestId,
        Guid adminUserId,
        string? reviewNote,
        CancellationToken cancellationToken = default);
}

public sealed record DokployAccessRequestResult(
    bool Ok,
    string? Error = null,
    DokployAccessRequest? Request = null);
