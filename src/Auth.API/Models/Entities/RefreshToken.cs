namespace Auth.API.Models.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }

    /// <summary>Bevares ved refresh så nye access tokens beholder samme <c>login_method</c>.</summary>
    public string? AuthMethod { get; set; }

    /// <summary>Krypteret Azure AD tokens (samme som auth code), til fornyelse og Graph.</summary>
    public string? ExternalOAuthTokensCipher { get; set; }

    public User User { get; set; } = null!;
}
