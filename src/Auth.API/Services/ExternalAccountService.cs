using System.Security.Claims;
using Auth.API.Data;
using Auth.API.Models;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class ExternalAccountService(AuthDbContext db, TimeProvider time) : IExternalAccountService
{
    public async Task<Guid> FindOrLinkUserAsync(
        ClaimsPrincipal externalPrincipal,
        string provider,
        UserEmailKind emailKind,
        CancellationToken cancellationToken = default)
    {
        var providerUserId = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("External login mangler NameIdentifier.");
        var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
        var displayName = externalPrincipal.FindFirstValue(ClaimTypes.Name)
            ?? externalPrincipal.Identity?.Name
            ?? email
            ?? providerUserId;

        var existing = await db.ExternalLogins
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == providerUserId, cancellationToken);

        if (existing is not null)
        {
            existing.User.LastLoginAt = time.GetUtcNow().UtcDateTime;
            await ApplyEmailLinkAsync(existing.UserId, email, emailKind, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, existing.UserId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return existing.UserId;
        }

        var norm = EmailNormalizer.Normalize(email);
        User? match = null;
        if (norm is not null)
        {
            match = await db.UserEmails
                .Where(e => e.NormalizedEmail == norm)
                .Select(e => e.User)
                .FirstOrDefaultAsync(cancellationToken);

            if (match is null)
            {
                // Ikke kald EmailNormalizer i LINQ — EF kan ikke oversætte det. Trim+ToLower matcher Normalize() for almindelige e-mails.
                match = await db.Users
                    .Include(u => u.LocalLogin)
                    .FirstOrDefaultAsync(
                        u => u.Email != null && u.Email.Trim().ToLower() == norm
                             || u.LocalLogin != null && u.LocalLogin.Email.Trim().ToLower() == norm,
                        cancellationToken);
            }
        }

        var now = time.GetUtcNow().UtcDateTime;
        User user;
        if (match is not null)
        {
            user = match;
            user.LastLoginAt = now;
            if (string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(email))
                user.Email = email;
        }
        else
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = displayName,
                Email = string.IsNullOrEmpty(email) ? null : email,
                EmailConfirmed = !string.IsNullOrEmpty(email),
                CreatedAt = now,
                LastLoginAt = now,
            };
            db.Users.Add(user);
            var userRole = await db.Roles.FirstAsync(r => r.Name == "User", cancellationToken);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id });
        }

        db.ExternalLogins.Add(new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = providerUserId,
            ProviderEmail = email,
            ProviderDisplayName = displayName,
            LinkedAt = now,
        });

        await ApplyEmailLinkAsync(user.Id, email, emailKind, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    public async Task<LinkExternalOutcome> LinkExternalToUserAsync(
        Guid targetUserId,
        ClaimsPrincipal externalPrincipal,
        string provider,
        UserEmailKind emailKind,
        CancellationToken cancellationToken = default)
    {
        var providerUserId = externalPrincipal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("External login mangler NameIdentifier.");
        var email = externalPrincipal.FindFirstValue(ClaimTypes.Email);
        var displayName = externalPrincipal.FindFirstValue(ClaimTypes.Name)
            ?? externalPrincipal.Identity?.Name
            ?? email
            ?? providerUserId;

        var targetExists = await db.Users.AnyAsync(
            u => u.Id == targetUserId && !u.IsDisabled,
            cancellationToken);
        if (!targetExists)
            throw new InvalidOperationException("Ukendt eller deaktiveret bruger.");

        var existing = await db.ExternalLogins
            .FirstOrDefaultAsync(
                x => x.Provider == provider && x.ProviderUserId == providerUserId,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.UserId != targetUserId)
                return LinkExternalOutcome.ConflictOtherUser;

            existing.ProviderEmail = email;
            existing.ProviderDisplayName = displayName;
            existing.LinkedAt = time.GetUtcNow().UtcDateTime;
            await db.Users
                .Where(u => u.Id == targetUserId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.LastLoginAt, time.GetUtcNow().UtcDateTime),
                    cancellationToken);
            await ApplyEmailLinkAsync(existing.UserId, email, emailKind, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, existing.UserId, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return LinkExternalOutcome.Linked;
        }

        var now = time.GetUtcNow().UtcDateTime;
        db.ExternalLogins.Add(new ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Provider = provider,
            ProviderUserId = providerUserId,
            ProviderEmail = email,
            ProviderDisplayName = displayName,
            LinkedAt = now,
        });

        await db.Users
            .Where(u => u.Id == targetUserId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastLoginAt, now),
                cancellationToken);
        await ApplyEmailLinkAsync(targetUserId, email, emailKind, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, targetUserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return LinkExternalOutcome.Linked;
    }

    public async Task<UnlinkExternalLoginResult> UnlinkExternalLoginAsync(
        Guid currentUserId,
        Guid externalLoginId,
        CancellationToken cancellationToken = default)
    {
        var row = await db.ExternalLogins
            .FirstOrDefaultAsync(
                e => e.Id == externalLoginId && e.UserId == currentUserId,
                cancellationToken);
        if (row is null)
            return UnlinkExternalLoginResult.NotFound;

        var extCount = await db.ExternalLogins.CountAsync(
            e => e.UserId == currentUserId,
            cancellationToken);
        var hasLocal = await db.LocalLogins.AnyAsync(
            l => l.UserId == currentUserId,
            cancellationToken);
        // Mindst én login-metode: andre ExternalLogins eller e-mail/adgangskode.
        if (extCount <= 1 && !hasLocal)
            return UnlinkExternalLoginResult.CannotRemoveLastLoginMethod;

        db.ExternalLogins.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return UnlinkExternalLoginResult.Success;
    }

    private async Task ApplyEmailLinkAsync(Guid userId, string? email, UserEmailKind kind, CancellationToken ct)
    {
        var norm = EmailNormalizer.Normalize(email);
        if (norm is null)
            return;

        var onOtherUser = await db.UserEmails.AnyAsync(e => e.NormalizedEmail == norm && e.UserId != userId, ct);
        if (onOtherUser)
            return;

        var alreadySameAddress = await db.UserEmails.AnyAsync(e => e.UserId == userId && e.NormalizedEmail == norm, ct);
        if (alreadySameAddress)
            return;

        var slot = await db.UserEmails.FirstOrDefaultAsync(e => e.UserId == userId && e.Kind == kind, ct);
        var now = time.GetUtcNow().UtcDateTime;
        if (slot is not null)
        {
            slot.NormalizedEmail = norm;
            slot.LinkedAt = now;
        }
        else
        {
            db.UserEmails.Add(new UserEmail
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                NormalizedEmail = norm,
                Kind = kind,
                LinkedAt = now,
            });
        }
    }
}
