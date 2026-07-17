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
        var opts = options.Value;
        if (!opts.Enabled || !wantDokploy)
            return;

        var email = user.Email;
        var norm = EmailNormalizer.Normalize(email);
        if (norm is null)
        {
            logger.LogWarning("Dokploy-provision sprunget over for {UserId}: mangler e-mail", user.Id);
            return;
        }

        var link = await db.DokployUserLinks.FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
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
            var match = users.FirstOrDefault(u =>
                EmailNormalizer.Normalize(u.Email) == norm);
            var dokployId = match?.Id ?? match?.UserId;

            if (string.IsNullOrWhiteSpace(dokployId))
            {
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dokploy-provision fejlede for Auth-bruger {UserId}", user.Id);
            link.LastError = ex.Message.Length <= 1000 ? ex.Message : ex.Message[..1000];
            link.IsProvisioned = false;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Kunne ikke gemme Dokploy-provision-fejl for {UserId}", user.Id);
            }
        }
    }
}
