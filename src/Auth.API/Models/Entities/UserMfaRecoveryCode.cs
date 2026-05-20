namespace Auth.API.Models.Entities;

public class UserMfaRecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }

    public UserTotpMfa Totp { get; set; } = null!;
}
