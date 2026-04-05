namespace Auth.API.Models.Entities;

public class AuthorizationCode
{
    public Guid Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string ClientStringId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }

    /// <summary>Login-metode på tidspunktet for /oauth/authorize (så /oauth/token ikke behøver cookie).</summary>
    public string? LoginMethod { get; set; }

    /// <summary>Krypteret Azure AD-token payload til Graph, kun ved Microsoft-login.</summary>
    public string? ExternalOAuthTokensCipher { get; set; }

    public User User { get; set; } = null!;
}
