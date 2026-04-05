using Auth.API.Models;

namespace Auth.API.Models.Entities;

public class UserEmail
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Trimmet lowercase — én global identitet pr. adresse på tværs af brugere.</summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    public UserEmailKind Kind { get; set; }
    public DateTime LinkedAt { get; set; }
}
