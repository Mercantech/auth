namespace Auth.API.Models.Entities;

public class UserTotpMfa
{
    public Guid UserId { get; set; }
    public string SecretCipher { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<UserMfaRecoveryCode> RecoveryCodes { get; set; } = new List<UserMfaRecoveryCode>();
}
