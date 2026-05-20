using Auth.API.Data;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Controllers;

[ApiController]
[Route("api/admin/usage")]
[EnableCors("MercantecSpa")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminUsageController(AuthDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<UsageSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        var since30 = DateTime.UtcNow.AddDays(-30);

        var byClient = await db.UserClientUsages
            .AsNoTracking()
            .GroupBy(u => u.ClientId)
            .Select(g => new ClientUsageSummaryRow(
                g.Key,
                g.Count(),
                g.Sum(x => x.AuthorizeCount),
                g.Sum(x => x.TokenExchangeCount),
                g.Sum(x => x.RefreshCount),
                g.Max(x => x.LastSeenAtUtc)))
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

        var byEventType = await db.AuthUsageEvents
            .AsNoTracking()
            .Where(e => e.CreatedAtUtc >= since30)
            .GroupBy(e => e.EventType)
            .Select(g => new EventTypeCountRow(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var recentEvents = await db.AuthUsageEvents
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(50)
            .Select(e => new UsageEventRow(
                e.Id,
                e.CreatedAtUtc,
                e.EventType,
                e.UserId,
                e.ClientId,
                e.Provider,
                e.LoginMethod))
            .ToListAsync(cancellationToken);

        var activeUsers30 = await db.AuthUsageEvents
            .AsNoTracking()
            .Where(e => e.UserId != null && e.CreatedAtUtc >= since30)
            .Select(e => e.UserId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        return Ok(new UsageSummaryResponse(
            byClient,
            byEventType,
            recentEvents,
            activeUsers30));
    }

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<UsageEventRow>>> Events(
        [FromQuery] Guid? userId,
        [FromQuery] string? clientId,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        var q = db.AuthUsageEvents.AsNoTracking().AsQueryable();
        if (userId is { } uid)
            q = q.Where(e => e.UserId == uid);
        if (!string.IsNullOrWhiteSpace(clientId))
            q = q.Where(e => e.ClientId == clientId.Trim());
        if (!string.IsNullOrWhiteSpace(eventType))
            q = q.Where(e => e.EventType == eventType.Trim());

        var rows = await q
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(limit)
            .Select(e => new UsageEventRow(
                e.Id,
                e.CreatedAtUtc,
                e.EventType,
                e.UserId,
                e.ClientId,
                e.Provider,
                e.LoginMethod))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }
}

public sealed record UsageSummaryResponse(
    IReadOnlyList<ClientUsageSummaryRow> ByClient,
    IReadOnlyList<EventTypeCountRow> EventsLast30Days,
    IReadOnlyList<UsageEventRow> RecentEvents,
    int ActiveUsersLast30Days);

public sealed record ClientUsageSummaryRow(
    string ClientId,
    int DistinctUsers,
    int TotalAuthorizes,
    int TotalTokenExchanges,
    int TotalRefreshes,
    DateTime LastSeenAtUtc);

public sealed record EventTypeCountRow(string EventType, int Count);

public sealed record UsageEventRow(
    Guid Id,
    DateTime CreatedAtUtc,
    string EventType,
    Guid? UserId,
    string? ClientId,
    string? Provider,
    string? LoginMethod);
