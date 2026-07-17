namespace Auth.API.Models.Entities;

public class DokployProjectGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string DokployProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public DateTime GrantedAtUtc { get; set; }
}
