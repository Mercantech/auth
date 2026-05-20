using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

/// <summary>Synkroniserer <see cref="User.Email"/> fra <see cref="UserEmail"/>-rækker (samme logik som i <see cref="ExternalAccountService"/>).</summary>
public static class UserPrimaryEmailSync
{
    public static async Task SyncFromLinkedEmailsAsync(AuthDbContext db, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .Include(u => u.LinkedEmails)
            .FirstAsync(u => u.Id == userId, cancellationToken);

        var personal = user.LinkedEmails
            .Where(e => e.Kind == UserEmailKind.Personal)
            .OrderByDescending(e => e.LinkedAt)
            .Select(e => e.NormalizedEmail)
            .FirstOrDefault();

        if (personal is not null)
        {
            user.Email = personal;
            return;
        }

        var fallback = user.LinkedEmails
            .OrderByDescending(e => e.LinkedAt)
            .Select(e => e.NormalizedEmail)
            .FirstOrDefault();

        if (fallback is not null)
            user.Email = fallback;
    }
}
