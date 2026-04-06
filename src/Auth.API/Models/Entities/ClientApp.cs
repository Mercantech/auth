namespace Auth.API.Models.Entities;

public class ClientApp
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public bool RequirePkce { get; set; } = true;
    public string? ClientSecretHash { get; set; }
    public string? AllowedScopes { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ClientAppRedirectUri> RedirectUris { get; set; } = new List<ClientAppRedirectUri>();
}
