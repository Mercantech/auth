using Auth.API.Data;
using Auth.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class AccountMergeService(AuthDbContext db) : IAccountMergeService
{
    public async Task<AccountMergeResult> MergeUsersAsync(
        Guid survivorUserId,
        Guid donorUserId,
        CancellationToken cancellationToken = default)
    {
        if (survivorUserId == donorUserId)
            return new AccountMergeResult(false, AccountMergeFailureReason.SameUser, null, []);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var survivor = await db.Users
                .Include(u => u.ExternalLogins)
                .Include(u => u.LinkedEmails)
                .Include(u => u.UserRoles)
                .Include(u => u.LocalLogin)
                .FirstOrDefaultAsync(u => u.Id == survivorUserId, cancellationToken);
            if (survivor is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new AccountMergeResult(false, AccountMergeFailureReason.SurvivorNotFound, null, []);
            }

            if (survivor.IsDisabled)
            {
                await tx.RollbackAsync(cancellationToken);
                return new AccountMergeResult(false, AccountMergeFailureReason.SurvivorDisabled, survivorUserId,
                    []);
            }

            var donor = await db.Users
                .Include(u => u.ExternalLogins)
                .Include(u => u.LinkedEmails)
                .Include(u => u.UserRoles)
                .Include(u => u.LocalLogin)
                .FirstOrDefaultAsync(u => u.Id == donorUserId, cancellationToken);
            if (donor is null)
            {
                await tx.RollbackAsync(cancellationToken);
                return new AccountMergeResult(false, AccountMergeFailureReason.DonorNotFound, survivorUserId, []);
            }

            var warnings = new List<string>();

            foreach (var ext in donor.ExternalLogins.ToList())
                ext.UserId = survivor.Id;

            switch (survivor.LocalLogin, donor.LocalLogin)
            {
                case (not null, not null):
                    warnings.Add(
                        "Donor havde e-mail/adgangskode-login; den er fjernet. Survivor beholder sit eksisterende lokale login.");
                    db.LocalLogins.Remove(donor.LocalLogin);
                    break;
                case (null, not null):
                    donor.LocalLogin.UserId = survivor.Id;
                    break;
            }

            var survivorKinds = survivor.LinkedEmails.Select(e => e.Kind).ToHashSet();
            foreach (var email in donor.LinkedEmails.ToList())
            {
                if (survivorKinds.Contains(email.Kind))
                {
                    warnings.Add(
                        $"Donor havde en {email.Kind}-e-mail i UserEmails, som er fjernet (survivor har allerede en {email.Kind}-plads): {email.NormalizedEmail}");
                    db.UserEmails.Remove(email);
                    continue;
                }

                email.UserId = survivor.Id;
                survivorKinds.Add(email.Kind);
            }

            var survivorRoles = survivor.UserRoles.Select(r => r.RoleId).ToHashSet();
            foreach (var donorRole in donor.UserRoles.ToList())
            {
                if (!survivorRoles.Contains(donorRole.RoleId))
                {
                    db.UserRoles.Add(new UserRole { UserId = survivor.Id, RoleId = donorRole.RoleId });
                    survivorRoles.Add(donorRole.RoleId);
                }

                db.UserRoles.Remove(donorRole);
            }

            if (string.IsNullOrWhiteSpace(survivor.DisplayName) && !string.IsNullOrWhiteSpace(donor.DisplayName))
                survivor.DisplayName = donor.DisplayName;
            if (string.IsNullOrWhiteSpace(survivor.AvatarUrl) && !string.IsNullOrWhiteSpace(donor.AvatarUrl))
                survivor.AvatarUrl = donor.AvatarUrl;
            survivor.EmailConfirmed |= donor.EmailConfirmed;
            if (survivor.LastLoginAt < donor.LastLoginAt)
                survivor.LastLoginAt = donor.LastLoginAt;

            await db.RefreshTokens
                .Where(t => t.UserId == survivor.Id || t.UserId == donor.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db.AuthorizationCodes
                .Where(c => c.UserId == survivor.Id || c.UserId == donor.Id)
                .ExecuteDeleteAsync(cancellationToken);

            db.Users.Remove(donor);
            await db.SaveChangesAsync(cancellationToken);

            await UserPrimaryEmailSync.SyncFromLinkedEmailsAsync(db, survivor.Id, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);
            return new AccountMergeResult(true, AccountMergeFailureReason.None, survivor.Id, warnings);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
