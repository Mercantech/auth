using Auth.API.Data;
using Auth.API.Models.Entities;
using Auth.API.Security;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Services;

public class AuthUsageTracker(AuthDbContext db, TimeProvider time) : IAuthUsageTracker
{
    public Task RecordProviderLoginAsync(
        Guid userId,
        string provider,
        string loginMethod,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.ProviderLogin,
            userId,
            clientId: null,
            provider,
            loginMethod,
            redirectUri: null,
            scope: null,
            touchClient: null,
            cancellationToken);

    public Task RecordPasswordLoginAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.PasswordLogin,
            userId,
            null,
            null,
            MercantecAuthClaims.LoginMethodValues.Password,
            null,
            null,
            null,
            cancellationToken);

    public Task RecordPasswordSignupAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.PasswordSignup,
            userId,
            null,
            null,
            MercantecAuthClaims.LoginMethodValues.Password,
            null,
            null,
            null,
            cancellationToken);

    public Task RecordPasswordLinkAsync(Guid userId, CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.PasswordLink,
            userId,
            null,
            null,
            MercantecAuthClaims.LoginMethodValues.Password,
            null,
            null,
            null,
            cancellationToken);

    public Task RecordAccountLinkAsync(
        Guid userId,
        string provider,
        string loginMethod,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.AccountLink,
            userId,
            null,
            provider,
            loginMethod,
            null,
            null,
            null,
            cancellationToken);

    public Task RecordOAuthAuthorizeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string? scope,
        string? loginMethod,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.OAuthAuthorize,
            userId,
            clientId,
            null,
            loginMethod,
            redirectUri,
            scope,
            ClientTouchKind.Authorize,
            cancellationToken);

    public Task RecordOAuthTokenExchangeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string? scope,
        string? loginMethod,
        CancellationToken cancellationToken = default) =>
        RecordAsync(
            AuthUsageEventTypes.OAuthTokenExchange,
            userId,
            clientId,
            null,
            loginMethod,
            redirectUri,
            scope,
            ClientTouchKind.TokenExchange,
            cancellationToken);

    public Task RecordOAuthRefreshAsync(
        Guid userId,
        string clientId,
        string? loginMethod,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return RecordAsync(
                AuthUsageEventTypes.OAuthRefresh,
                userId,
                null,
                null,
                loginMethod,
                null,
                null,
                null,
                cancellationToken);
        }

        return RecordAsync(
            AuthUsageEventTypes.OAuthRefresh,
            userId,
            clientId,
            null,
            loginMethod,
            null,
            null,
            ClientTouchKind.Refresh,
            cancellationToken);
    }

    private enum ClientTouchKind { Authorize, TokenExchange, Refresh }

    private async Task RecordAsync(
        string eventType,
        Guid userId,
        string? clientId,
        string? provider,
        string? loginMethod,
        string? redirectUri,
        string? scope,
        ClientTouchKind? touchClient,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;

        db.AuthUsageEvents.Add(new AuthUsageEvent
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = now,
            EventType = eventType,
            UserId = userId,
            ClientId = NormalizeClientId(clientId),
            Provider = NormalizeProvider(provider),
            LoginMethod = Truncate(loginMethod, 64),
            RedirectUri = Truncate(redirectUri, 512),
            Scope = Truncate(scope, 256),
        });

        if (!string.IsNullOrEmpty(provider))
        {
            await db.ExternalLogins
                .Where(e => e.UserId == userId && e.Provider == provider)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(e => e.LastUsedAtUtc, now),
                    cancellationToken);
        }

        if (touchClient is { } kind && !string.IsNullOrEmpty(clientId))
            await TouchUserClientUsageAsync(userId, clientId, kind, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task TouchUserClientUsageAsync(
        Guid userId,
        string clientId,
        ClientTouchKind kind,
        DateTime now,
        CancellationToken cancellationToken)
    {
        clientId = NormalizeClientId(clientId)!;
        var row = await db.UserClientUsages
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ClientId == clientId, cancellationToken);

        if (row is null)
        {
            row = new UserClientUsage
            {
                UserId = userId,
                ClientId = clientId,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
            };
            db.UserClientUsages.Add(row);
        }
        else
        {
            row.LastSeenAtUtc = now;
        }

        switch (kind)
        {
            case ClientTouchKind.Authorize:
                row.AuthorizeCount++;
                row.LastAuthorizeAtUtc = now;
                break;
            case ClientTouchKind.TokenExchange:
                row.TokenExchangeCount++;
                row.LastTokenExchangeAtUtc = now;
                break;
            case ClientTouchKind.Refresh:
                row.RefreshCount++;
                row.LastRefreshAtUtc = now;
                break;
        }
    }

    private static string? NormalizeClientId(string? clientId) =>
        string.IsNullOrWhiteSpace(clientId) ? null : clientId.Trim();

    private static string? NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? null : provider.Trim().ToLowerInvariant();

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];
}
