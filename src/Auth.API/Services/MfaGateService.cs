using Auth.API.Data;
using Auth.API.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public class MfaGateService(AuthDbContext db, IOptions<MfaOptions> mfaOptions) : IMfaGateService
{
    public async Task<bool> HasSecondFactorConfiguredAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var totp = await db.UserTotpMfas
            .AsNoTracking()
            .AnyAsync(t => t.UserId == userId && t.IsEnabled, cancellationToken);
        if (totp)
            return true;

        return await db.UserPasskeyCredentials
            .AsNoTracking()
            .AnyAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<bool> RequiresMfaStepAsync(
        Guid userId,
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        if (!await HasSecondFactorConfiguredAsync(userId, cancellationToken))
            return false;

        return true;
    }

    public Task<bool> RoleRequiresMfaSetupAsync(
        IReadOnlyList<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        var required = mfaOptions.Value.RequireForRoles;
        if (required.Length == 0)
            return Task.FromResult(false);

        var set = new HashSet<string>(required, StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(roleNames.Any(r => set.Contains(r)));
    }
}
