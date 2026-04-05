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
            await SyncUserPrimaryEmailAsync(existing.UserId, cancellationToken);
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
        await SyncUserPrimaryEmailAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
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

    private async Task SyncUserPrimaryEmailAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.LinkedEmails)
            .FirstAsync(u => u.Id == userId, ct);

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
