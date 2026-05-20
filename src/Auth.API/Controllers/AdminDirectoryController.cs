using Auth.API.Data;
using Auth.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Controllers;

/// <summary>Admin-API til brugerdata (Bearer JWT med rolle Admin).</summary>
[ApiController]
[Route("api/admin")]
[EnableCors("MercantecSpa")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminDirectoryController(AuthDbContext db, IAccountMergeService accountMerge)
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
            .OrderBy(u => u.DisplayName)
            .ToListAsync(cancellationToken);

        var rows = users.Select(u => new UserDirectoryRow(
            u.Id,
            u.DisplayName,
            u.Email,
            u.LocalLogin != null,
            u.ExternalLogins.Select(e => e.Provider).Distinct().OrderBy(p => p).ToList(),
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
}

public sealed record MergeUsersRequest(Guid SurvivorUserId, Guid DonorUserId);

public sealed record MergeUsersApiResponse(Guid SurvivorUserId, IReadOnlyList<string> Warnings);

public sealed record UserDirectoryRow(
    Guid Id,
    string DisplayName,
    string? Email,
    bool HasLocalLogin,
    IReadOnlyList<string> LinkedProviders,
    IReadOnlyList<LinkedEmailRow> LinkedEmails);

public sealed record LinkedEmailRow(string NormalizedEmail, string Kind);
