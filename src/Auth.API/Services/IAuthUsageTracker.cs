namespace Auth.API.Services;

public interface IAuthUsageTracker
{
    Task RecordProviderLoginAsync(
        Guid userId,
        string provider,
        string loginMethod,
        CancellationToken cancellationToken = default);

    Task RecordPasswordLoginAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordPasswordSignupAsync(Guid userId, CancellationToken cancellationToken = default);

    Task RecordAccountLinkAsync(
        Guid userId,
        string provider,
        string loginMethod,
        CancellationToken cancellationToken = default);

    Task RecordOAuthAuthorizeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string? scope,
        string? loginMethod,
        CancellationToken cancellationToken = default);

    Task RecordOAuthTokenExchangeAsync(
        Guid userId,
        string clientId,
        string redirectUri,
        string? scope,
        string? loginMethod,
        CancellationToken cancellationToken = default);

    Task RecordOAuthRefreshAsync(
        Guid userId,
        string clientId,
        string? loginMethod,
        CancellationToken cancellationToken = default);
}
