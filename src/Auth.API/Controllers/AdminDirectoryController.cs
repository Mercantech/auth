using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Security;
using Auth.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Controllers;

/// <summary>Admin-API til brugerdata (Admin-JWT eller MCP <c>x-api-key</c>).</summary>
[ApiController]
[Route("api/admin")]
[EnableCors("MercantecSpa")]
[Authorize(Policy = AdminApiPolicies.Name)]
public class AdminDirectoryController(
    AuthDbContext db,
    IAccountMergeService accountMerge,
    IUserDeletionService userDeletion)
    : ControllerBase
{
    [HttpGet("users-directory")]
    public async Task<ActionResult<IReadOnlyList<UserDirectoryRow>>> UsersDirectory(CancellationToken cancellationToken)
    {
        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.ExternalLogins)
            .Include(u => u.LinkedEmails)
            .Include(u => u.LocalLogin)
            .Include(u => u.ClientUsages)
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        var rows = users.Select(u => new UserDirectoryRow(
            u.Id,
            u.DisplayName,
            u.Email,
            u.LocalLogin != null,
            u.IsDisabled,
            u.ExternalLogins
                .OrderByDescending(e => e.LastUsedAtUtc ?? e.LinkedAt)
                .Select(e => new ProviderUsageRow(e.Provider, e.LastUsedAtUtc, e.LinkedAt))
                .ToList(),
            u.ClientUsages
                .OrderByDescending(c => c.LastSeenAtUtc)
                .Select(c => new ClientUsageRow(
                    c.ClientId,
                    c.AuthorizeCount,
                    c.TokenExchangeCount,
                    c.RefreshCount,
                    c.LastSeenAtUtc))
                .ToList(),
            u.LinkedEmails
                .OrderBy(e => e.Kind)
                .Select(e => new LinkedEmailRow(e.NormalizedEmail, e.Kind.ToString()))
                .ToList())).ToList();

        return Ok(rows);
    }

    /// <summary>Sammenlægger donor-brugeren ind i survivor (som beholder samme JWT <c>sub</c>). Alle refresh tokens og ikke-brugte auth codes på begge IDs invalideres.</summary>
    [HttpPost("users/merge")]
    public async Task<IActionResult> MergeUsers([FromBody] MergeUsersRequest body, CancellationToken cancellationToken)
    {
        var r = await accountMerge.MergeUsersAsync(body.SurvivorUserId, body.DonorUserId, cancellationToken);
        if (r.Success)
            return Ok(new MergeUsersApiResponse(r.SurvivorUserId!.Value, r.Warnings));

        return r.Failure switch
        {
            AccountMergeFailureReason.SameUser => BadRequest(new { error = "Survivor og donor må ikke være samme bruger." }),
            AccountMergeFailureReason.SurvivorNotFound =>
                Problem(statusCode: StatusCodes.Status404NotFound,
                    detail: "Survivor-brugeren blev ikke fundet."),
            AccountMergeFailureReason.DonorNotFound =>
                Problem(statusCode: StatusCodes.Status404NotFound,
                    detail: "Donor-brugeren blev ikke fundet."),
            AccountMergeFailureReason.SurvivorDisabled =>
                Problem(statusCode: StatusCodes.Status409Conflict,
                    detail: "Survivor-brugeren er deaktiveret — vælg en aktiv canonisk bruger først."),
            _ => Problem(),
        };
    }

    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> ListRoles(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("users/{userId:guid}/roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetUserRoles(Guid userId, CancellationToken cancellationToken)
    {
        var exists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            return NotFound(new { error = "Brugeren findes ikke." });

        var roles = await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>Erstatter brugerens roller. Ukendte rollenavne oprettes. Kan ikke fjerne Admin fra sidste Admin.</summary>
    [HttpPut("users/{userId:guid}/roles")]
    public async Task<IActionResult> SetUserRoles(Guid userId, [FromBody] SetUserRolesRequest body, CancellationToken cancellationToken)
    {
        var requested = (body.Roles ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return NotFound(new { error = "Brugeren findes ikke." });

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        if (adminRole is not null
            && user.UserRoles.Any(ur => ur.RoleId == adminRole.Id)
            && !requested.Contains("Admin", StringComparer.OrdinalIgnoreCase))
        {
            var otherAdmins = await db.UserRoles.CountAsync(
                ur => ur.RoleId == adminRole.Id && ur.UserId != userId,
                cancellationToken);
            if (otherAdmins == 0)
            {
                return Conflict(new
                {
                    error = "Sidste Administrator kan ikke miste Admin-rollen — tildel Admin til en anden bruger først.",
                });
            }
        }

        var rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var name in requested)
        {
            if (rolesByName.ContainsKey(name))
                continue;

            var created = new Models.Entities.Role { Id = Guid.NewGuid(), Name = name };
            db.Roles.Add(created);
            rolesByName[name] = created;
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var name in requested)
        {
            var role = rolesByName[name];
            db.UserRoles.Add(new Models.Entities.UserRole { UserId = userId, RoleId = role.Id });
        }

        await db.SaveChangesAsync(cancellationToken);

        var assigned = requested.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return Ok(new { userId, roles = assigned });
    }

    /// <summary>Fjerner en brugerkonto og alle associerede login-/session-data. Kan ikke fjerne sidste Admin eller egen konto.</summary>
    [HttpDelete("users/{userId:guid}")]
    public async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var actorUserId))
            return Unauthorized();

        var r = await userDeletion.DeleteUserAsync(userId, actorUserId, cancellationToken);
        if (r.Success)
            return NoContent();

        return r.Failure switch
        {
            UserDeletionFailureReason.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "Brugeren findes ikke."),
            UserDeletionFailureReason.CannotDeleteSelf => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Du kan ikke slette din egen konto herfra."),
            UserDeletionFailureReason.CannotDeleteLastAdmin => Conflict(new
            {
                error = "Sidste Administrator kan ikke fjernes — tildel Admin til en anden bruger først.",
            }),
            _ => Problem(),
        };
    }
}

public sealed record MergeUsersRequest(Guid SurvivorUserId, Guid DonorUserId);

public sealed record SetUserRolesRequest(IReadOnlyList<string>? Roles);

public sealed record MergeUsersApiResponse(Guid SurvivorUserId, IReadOnlyList<string> Warnings);

public sealed record UserDirectoryRow(
    Guid Id,
    string DisplayName,
    string? Email,
    bool HasLocalLogin,
    bool IsDisabled,
    IReadOnlyList<ProviderUsageRow> LinkedProviders,
    IReadOnlyList<ClientUsageRow> ClientUsages,
    IReadOnlyList<LinkedEmailRow> LinkedEmails);

public sealed record ProviderUsageRow(string Provider, DateTime? LastUsedAtUtc, DateTime LinkedAtUtc);

public sealed record ClientUsageRow(
    string ClientId,
    int AuthorizeCount,
    int TokenExchangeCount,
    int RefreshCount,
    DateTime LastSeenAtUtc);

public sealed record LinkedEmailRow(string NormalizedEmail, string Kind);
