using Auth.API.Data;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class UserRoleAdminService(AuthDbContext db) : IUserRoleAdminService
{
    private const string AdminRoleName = "Admin";

    public async Task<IReadOnlyList<string>> ListRoleNamesAsync(CancellationToken cancellationToken = default) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>?> GetUserRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var exists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
        if (!exists)
            return null;

        return await db.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role!.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRoleAdminResult> SetUserRolesAsync(
        Guid userId,
        IEnumerable<string>? roles,
        CancellationToken cancellationToken = default)
    {
        var requested = (roles ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var user = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return new UserRoleAdminResult(false, UserRoleAdminFailureReason.NotFound, []);

        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == AdminRoleName, cancellationToken);
        if (adminRole is not null
            && user.UserRoles.Any(ur => ur.RoleId == adminRole.Id)
            && !requested.Contains(AdminRoleName, StringComparer.OrdinalIgnoreCase))
        {
            var otherAdmins = await db.UserRoles.CountAsync(
                ur => ur.RoleId == adminRole.Id && ur.UserId != userId,
                cancellationToken);
            if (otherAdmins == 0)
            {
                return new UserRoleAdminResult(
                    false,
                    UserRoleAdminFailureReason.CannotRemoveLastAdmin,
                    []);
            }
        }

        var rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        foreach (var name in requested)
        {
            if (rolesByName.ContainsKey(name))
                continue;

            var created = new Role { Id = Guid.NewGuid(), Name = name };
            db.Roles.Add(created);
            rolesByName[name] = created;
        }

        db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var name in requested)
        {
            var role = rolesByName[name];
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        }

        await db.SaveChangesAsync(cancellationToken);

        var assigned = requested.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return new UserRoleAdminResult(true, UserRoleAdminFailureReason.None, assigned);
    }
}
