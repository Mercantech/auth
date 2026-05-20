namespace Auth.API.Models.Entities;

/// <summary>Append-only spor af auth-aktivitet (login, authorize, tokens).</summary>
public class AuthUsageEvent
{
    public Guid Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Se <see cref="AuthUsageEventTypes"/>.</summary>
    public string EventType { get; set; } = string.Empty;

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>OAuth <c>client_id</c> når relevant.</summary>
    public string? ClientId { get; set; }

    /// <summary>Ekstern udbyder (google, microsoft, …) ved provider-login.</summary>
    public string? Provider { get; set; }

    public string? LoginMethod { get; set; }
    public string? RedirectUri { get; set; }
    public string? Scope { get; set; }
}
