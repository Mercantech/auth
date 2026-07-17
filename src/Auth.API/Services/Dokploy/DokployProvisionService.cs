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
        string? dokployPassword,
        CancellationToken cancellationToken = default)
    {
        if (!wantDokploy)
            return;

        try
        {
            await ProvisionCoreAsync(user, dokployPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dokploy-provision kastede uventet for {UserId}", user.Id);
        }
    }

    public async Task<DokployProvisionResult> ProvisionAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
            return new DokployProvisionResult(DokployProvisionStatus.Failed, ErrorMessage: "Bruger findes ikke.");

        return await ProvisionCoreAsync(user, password, cancellationToken);
    }

    private async Task<DokployProvisionResult> ProvisionCoreAsync(
        User user,
        string? password,
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

        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 100)
            return new DokployProvisionResult(DokployProvisionStatus.InvalidPassword);

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
            var match = FindByEmail(users, norm);
            var dokployId = ResolveUserId(match);
            var createdNew = false;

            if (string.IsNullOrWhiteSpace(dokployId))
            {
                createdNew = true;
                await api.CreateUserWithCredentialsAsync(
                    email!,
                    password,
                    opts.MemberRole,
                    cancellationToken);

                users = await api.ListUsersAsync(cancellationToken);
                match = FindByEmail(users, norm);
                dokployId = ResolveUserId(match);

                if (string.IsNullOrWhiteSpace(dokployId))
                {
                    logger.LogWarning(
                        "Dokploy-bruger oprettet for {Email}, men userId ikke fundet i user.all ({Count} brugere parset)",
                        email,
                        users.Count);
                }
            }

            link.DokployUserId = string.IsNullOrWhiteSpace(dokployId) ? null : dokployId;
            // Oprettelse lykkedes også uden id (ACL sync finder senere via e-mail).
            link.IsProvisioned = createdNew || !string.IsNullOrWhiteSpace(dokployId);
            link.ProvisionedAtUtc = time.GetUtcNow().UtcDateTime;
            link.LastError = null;
            await db.SaveChangesAsync(cancellationToken);

            if (!createdNew)
                return new DokployProvisionResult(DokployProvisionStatus.LinkedExisting, link.DokployUserId);

            return new DokployProvisionResult(DokployProvisionStatus.Created, link.DokployUserId);
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

    private static DokployUserDto? FindByEmail(IReadOnlyList<DokployUserDto> users, string normalizedEmail)
        => users.FirstOrDefault(u => EmailNormalizer.Normalize(u.Email) == normalizedEmail);

    private static string? ResolveUserId(DokployUserDto? user)
        => user?.ResolvedUserId;

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
