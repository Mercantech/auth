using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class UserActionTokenService(AuthDbContext db, TimeProvider time) : IUserActionTokenService
{
    public async Task<string> IssueAsync(
        Guid userId,
        UserActionTokenPurpose purpose,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;

        await db.UserActionTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.ConsumedAtUtc, now),
                cancellationToken);

        var plain = SecureToken.CreateOpaqueToken();
        db.UserActionTokens.Add(new UserActionToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = SecureToken.HashOpaqueToken(plain),
            Purpose = purpose,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
        });
        await db.SaveChangesAsync(cancellationToken);
        return plain;
    }

    public async Task<User?> ValidateAndConsumeAsync(
        string plainToken,
        UserActionTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
            return null;

        var hash = SecureToken.HashOpaqueToken(plainToken);
        var now = time.GetUtcNow().UtcDateTime;

        var row = await db.UserActionTokens
            .Include(t => t.User)
            .ThenInclude(u => u!.LocalLogin)
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash
                     && t.Purpose == purpose
                     && t.ConsumedAtUtc == null
                     && t.ExpiresAtUtc > now,
                cancellationToken);

        if (row?.User is null)
            return null;

        row.ConsumedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
        return row.User;
    }
}
