using Auth.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class UserDeletionService(AuthDbContext db) : IUserDeletionService
{
    private const string AdminRoleName = "Admin";

    public async Task<UserDeletionResult> DeleteUserAsync(
        Guid userIdToDelete,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (userIdToDelete == actorUserId)
            return new UserDeletionResult(false, UserDeletionFailureReason.CannotDeleteSelf);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var user = await db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.ExternalLogins)
                .Include(u => u.LinkedEmails)
                .Include(u => u.LocalLogin)
                .FirstOrDefaultAsync(u => u.Id == userIdToDelete, cancellationToken);

            if (user is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new UserDeletionResult(false, UserDeletionFailureReason.NotFound);
            }

            if (user.UserRoles.Any(ur => string.Equals(ur.Role.Name, AdminRoleName, StringComparison.Ordinal)))
            {
                var adminRoleId = await db.Roles.AsNoTracking()
                    .Where(r => r.Name == AdminRoleName)
                    .Select(r => r.Id)
                    .FirstAsync(cancellationToken);
                var adminCount = await db.UserRoles.CountAsync(
                    ur => ur.RoleId == adminRoleId,
                    cancellationToken);
                if (adminCount <= 1)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return new UserDeletionResult(false, UserDeletionFailureReason.CannotDeleteLastAdmin);
                }
            }

            await db.AuthorizationCodes
                .Where(c => c.UserId == userIdToDelete)
                .ExecuteDeleteAsync(cancellationToken);
            await db.RefreshTokens
                .Where(t => t.UserId == userIdToDelete)
                .ExecuteDeleteAsync(cancellationToken);
            await db.AuthUsageEvents
                .Where(e => e.UserId == userIdToDelete)
                .ExecuteDeleteAsync(cancellationToken);
            await db.UserClientUsages
                .Where(u => u.UserId == userIdToDelete)
                .ExecuteDeleteAsync(cancellationToken);

            db.ExternalLogins.RemoveRange(user.ExternalLogins);
            if (user.LocalLogin is not null)
                db.LocalLogins.Remove(user.LocalLogin);
            db.UserEmails.RemoveRange(user.LinkedEmails);
            db.UserRoles.RemoveRange(user.UserRoles);
            db.Users.Remove(user);

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return new UserDeletionResult(true, UserDeletionFailureReason.None);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
