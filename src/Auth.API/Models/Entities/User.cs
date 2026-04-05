namespace Auth.API.Models.Entities;

public class User
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailConfirmed { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
    public bool IsDisabled { get; set; }

    /// <summary>Sidste fulde login (cookie-session), fx <c>microsoft-work</c>. Bruges som fallback når auth code oprettes.</summary>
    public string? LastLoginMethod { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<ExternalLogin> ExternalLogins { get; set; } = new List<ExternalLogin>();
    public ICollection<UserEmail> LinkedEmails { get; set; } = new List<UserEmail>();
    public LocalLogin? LocalLogin { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
