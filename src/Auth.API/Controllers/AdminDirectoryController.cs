using Auth.API.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Controllers;

/// <summary>Admin-API til oversigt over brugere (Bearer JWT med rolle Admin).</summary>
[ApiController]
[Route("api/admin")]
[EnableCors("MercantecSpa")]
public class AdminDirectoryController(AuthDbContext db) : ControllerBase
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
}

public sealed record UserDirectoryRow(
    Guid Id,
    string DisplayName,
    string? Email,
    bool HasLocalLogin,
    IReadOnlyList<string> LinkedProviders,
    IReadOnlyList<LinkedEmailRow> LinkedEmails);

public sealed record LinkedEmailRow(string NormalizedEmail, string Kind);
