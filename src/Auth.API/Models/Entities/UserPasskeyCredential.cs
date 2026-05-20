namespace Auth.API.Models.Entities;

public class UserPasskeyCredential
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public byte[] CredentialId { get; set; } = [];
    public byte[] PublicKey { get; set; } = [];
    public uint SignCount { get; set; }
    public Guid AaGuid { get; set; }
    public string? Transports { get; set; }
    public string FriendlyName { get; set; } = "Passkey";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }

    public User User { get; set; } = null!;
}
