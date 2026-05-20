namespace Auth.API.Models.Entities;

/// <summary>Sammenlagt brug per bruger og OAuth-klient.</summary>
public class UserClientUsage
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string ClientId { get; set; } = string.Empty;

    public DateTime FirstSeenAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public int AuthorizeCount { get; set; }
    public int TokenExchangeCount { get; set; }
    public int RefreshCount { get; set; }

    public DateTime? LastAuthorizeAtUtc { get; set; }
    public DateTime? LastTokenExchangeAtUtc { get; set; }
    public DateTime? LastRefreshAtUtc { get; set; }
}
