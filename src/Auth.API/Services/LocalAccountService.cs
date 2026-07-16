using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class LocalAccountService(AuthDbContext db, TimeProvider time) : ILocalAccountService
{
    public Task<User?> FindUserForPasswordSignInAsync(string email, CancellationToken cancellationToken = default) =>
        FindUserByNormalizedEmailAsync(email, includeRoles: true, cancellationToken);

    public Task<User?> FindUserForPasswordLinkByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        FindUserByNormalizedEmailAsync(email, includeRoles: false, cancellationToken);

    public async Task<SetPasswordResult> SetPasswordAsync(
        Guid userId,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var norm = EmailNormalizer.Normalize(email);
        if (norm is null || password.Length < 8 || password.Length > 100)
            return SetPasswordResult.InvalidEmail;

        var user = await db.Users
            .Include(u => u.LocalLogin)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return SetPasswordResult.UserNotFound;
        if (user.IsDisabled)
            return SetPasswordResult.UserDisabled;

        if (!await EmailBelongsToUserAsync(userId, norm, cancellationToken))
            return SetPasswordResult.EmailNotOwnedByUser;

        var hadLocal = user.LocalLogin is not null;
        var now = time.GetUtcNow().UtcDateTime;
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        if (!hadLocal)
        {
            db.LocalLogins.Add(new LocalLogin
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Email = email.Trim(),
                PasswordHash = hash,
                CreatedAt = now,
            });
        }
        else
        {
            user.LocalLogin!.Email = email.Trim();
            user.LocalLogin.PasswordHash = hash;
        }

        await EnsurePersonalUserEmailAsync(userId, norm, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, userId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return hadLocal ? SetPasswordResult.Updated : SetPasswordResult.Created;
    }

    public async Task<User> CreateUserWithPasswordAsync(
        string displayName,
        string email,
        string password,
        string? createdViaClientId = null,
        CancellationToken cancellationToken = default)
    {
        var norm = EmailNormalizer.Normalize(email)
            ?? throw new ArgumentException("Ugyldig e-mail.", nameof(email));
        var now = time.GetUtcNow().UtcDateTime;

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName.Trim(),
            Email = email.Trim(),
            EmailConfirmed = false,
            CreatedAt = now,
            LastLoginAt = now,
            LastLoginMethod = MercantecAuthClaims.LoginMethodValues.Password,
            CreatedViaClientId = string.IsNullOrWhiteSpace(createdViaClientId) ? null : createdViaClientId.Trim(),
        };
        db.Users.Add(user);
        db.LocalLogins.Add(new LocalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Email = email.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = now,
        });
        db.UserEmails.Add(new UserEmail
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            NormalizedEmail = norm,
            Kind = UserEmailKind.Personal,
            LinkedAt = now,
        });

        var userRole = await db.Roles.FirstAsync(r => r.Name == "User", cancellationToken);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        await db.SaveChangesAsync(cancellationToken);
        await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<User?> FindUserByNormalizedEmailAsync(
        string email,
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        var norm = EmailNormalizer.Normalize(email);
        if (norm is null)
            return null;

        IQueryable<User> ApplyIncludes(IQueryable<User> q)
        {
            q = q.Include(u => u.LocalLogin);
            if (includeRoles)
                q = q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role);
            return q;
        }

        var user = await ApplyIncludes(db.Users)
            .Where(u => u.LinkedEmails.Any(e => e.NormalizedEmail == norm))
            .FirstOrDefaultAsync(cancellationToken);
        if (user is not null)
            return user;

        user = await ApplyIncludes(db.Users).FirstOrDefaultAsync(
            u => u.LocalLogin != null && u.LocalLogin.Email.Trim().ToLower() == norm,
            cancellationToken);
        if (user is not null)
            return user;

        return await ApplyIncludes(db.Users).FirstOrDefaultAsync(
            u => u.Email != null && u.Email.Trim().ToLower() == norm,
            cancellationToken);
    }

    private async Task<bool> EmailBelongsToUserAsync(Guid userId, string norm, CancellationToken cancellationToken)
    {
        if (await db.UserEmails.AnyAsync(
                e => e.UserId == userId && e.NormalizedEmail == norm,
                cancellationToken))
            return true;

        if (await db.Users.AnyAsync(
                u => u.Id == userId && u.Email != null && u.Email.Trim().ToLower() == norm,
                cancellationToken))
            return true;

        if (await db.LocalLogins.AnyAsync(
                l => l.UserId == userId && l.Email.Trim().ToLower() == norm,
                cancellationToken))
            return true;

        return await db.ExternalLogins.AnyAsync(
            e => e.UserId == userId
                 && e.ProviderEmail != null
                 && e.ProviderEmail.Trim().ToLower() == norm,
            cancellationToken);
    }

    private async Task EnsurePersonalUserEmailAsync(
        Guid userId,
        string norm,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var onOther = await db.UserEmails.AnyAsync(
            e => e.NormalizedEmail == norm && e.UserId != userId,
            cancellationToken);
        if (onOther)
            return;

        var slot = await db.UserEmails.FirstOrDefaultAsync(
            e => e.UserId == userId && e.Kind == UserEmailKind.Personal,
            cancellationToken);
        if (slot is not null)
        {
            slot.NormalizedEmail = norm;
            slot.LinkedAt = now;
            return;
        }

        if (await db.UserEmails.AnyAsync(e => e.UserId == userId && e.NormalizedEmail == norm, cancellationToken))
            return;

        db.UserEmails.Add(new UserEmail
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NormalizedEmail = norm,
            Kind = UserEmailKind.Personal,
            LinkedAt = now,
        });
    }
}
