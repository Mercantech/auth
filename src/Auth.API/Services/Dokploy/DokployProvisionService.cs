using System.Security.Cryptography;
using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Options;
using Auth.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services.Dokploy;

public sealed class DokployProvisionService(
    AuthDbContext db,
    IDokployApiClient api,
    IOptions<DokployOptions> options,
    TimeProvider time,
    ILogger<DokployProvisionService> logger) : IDokployProvisionService
{
    public async Task TryProvisionIfRequestedAsync(
        User user,
        bool wantDokploy,
        CancellationToken cancellationToken = default)
    {
        if (!wantDokploy)
            return;

        try
        {
            await ProvisionCoreAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dokploy-provision kastede uventet for {UserId}", user.Id);
        }
    }

    public async Task<DokployProvisionResult> ProvisionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return new DokployProvisionResult(DokployProvisionStatus.Failed, ErrorMessage: "Bruger findes ikke.");

        return await ProvisionCoreAsync(user, cancellationToken);
    }

    private async Task<DokployProvisionResult> ProvisionCoreAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var opts = options.Value;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.ApiKey))
            return new DokployProvisionResult(DokployProvisionStatus.Disabled);

        var email = await ResolveEmailAsync(user, cancellationToken);
        var norm = EmailNormalizer.Normalize(email);
        if (norm is null)
        {
            logger.LogWarning("Dokploy-provision sprunget over for {UserId}: mangler e-mail", user.Id);
            return new DokployProvisionResult(DokployProvisionStatus.MissingEmail);
        }

        var link = await db.DokployUserLinks.FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (link is { IsProvisioned: true } && !string.IsNullOrWhiteSpace(link.DokployUserId))
            return new DokployProvisionResult(DokployProvisionStatus.AlreadyProvisioned, link.DokployUserId);

        if (link is null)
        {
            link = new DokployUserLink
            {
                UserId = user.Id,
                LinkedEmail = norm,
            };
            db.DokployUserLinks.Add(link);
        }
        else
        {
            link.LinkedEmail = norm;
        }

        try
        {
            var users = await api.ListUsersAsync(cancellationToken);
            var match = users.FirstOrDefault(u => EmailNormalizer.Normalize(u.Email) == norm);
            var dokployId = match?.Id ?? match?.UserId;
            var createdNew = false;

            if (string.IsNullOrWhiteSpace(dokployId))
            {
                createdNew = true;
                try
                {
                    await api.InviteMemberAsync(email!, opts.MemberRole, cancellationToken);
                }
                catch (DokployApiException ex)
                {
                    logger.LogWarning(
                        ex,
                        "Dokploy invite fejlede for {Email}; prøver createUserWithCredentials",
                        email);
                    var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
                    await api.CreateUserWithCredentialsAsync(
                        email!,
                        password,
                        opts.MemberRole,
                        cancellationToken);
                }

                users = await api.ListUsersAsync(cancellationToken);
                match = users.FirstOrDefault(u => EmailNormalizer.Normalize(u.Email) == norm);
                dokployId = match?.Id ?? match?.UserId;
            }

            link.DokployUserId = string.IsNullOrWhiteSpace(dokployId) ? null : dokployId;
            link.IsProvisioned = !string.IsNullOrWhiteSpace(dokployId);
            link.ProvisionedAtUtc = time.GetUtcNow().UtcDateTime;
            link.LastError = link.IsProvisioned
                ? null
                : "Bruger oprettet/inviteret, men Dokploy-userId kunne ikke findes endnu.";
            await db.SaveChangesAsync(cancellationToken);

            if (!link.IsProvisioned)
            {
                return new DokployProvisionResult(
                    DokployProvisionStatus.Failed,
                    ErrorMessage: link.LastError);
            }

            return new DokployProvisionResult(
                createdNew ? DokployProvisionStatus.InvitedOrCreated : DokployProvisionStatus.LinkedExisting,
                link.DokployUserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dokploy-provision fejlede for Auth-bruger {UserId}", user.Id);
            link.LastError = Truncate(ex.Message);
            link.IsProvisioned = false;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Kunne ikke gemme Dokploy-provision-fejl for {UserId}", user.Id);
            }

            return new DokployProvisionResult(DokployProvisionStatus.Failed, ErrorMessage: Truncate(ex.Message));
        }
    }

    private async Task<string?> ResolveEmailAsync(User user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.Email))
            return user.Email;

        var local = await db.LocalLogins
            .AsNoTracking()
            .Where(l => l.UserId == user.Id)
            .Select(l => l.Email)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(local))
            return local;

        return await db.UserEmails
            .AsNoTracking()
            .Where(e => e.UserId == user.Id)
            .OrderByDescending(e => e.LinkedAt)
            .Select(e => e.NormalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string Truncate(string message)
        => message.Length <= 1000 ? message : message[..1000];
}
