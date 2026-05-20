namespace Auth.API.Models.Entities;

public class ExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public string? ProviderDisplayName { get; set; }
    public DateTime LinkedAt { get; set; }

    /// <summary>Sidste login via denne udbyder (opdateres ved hvert provider-login).</summary>
    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
