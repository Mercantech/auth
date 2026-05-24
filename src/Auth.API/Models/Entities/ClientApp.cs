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
    /// <summary>Preset-id for OAuth login-UI (fx mercanlink). Null = Mercantec standard.</summary>
    public string? LoginThemeId { get; set; }
    /// <summary>Komma-separeret whitelist af login-metoder (passkey, password, google, …). Null = alle server-aktiverede.</summary>
    public string? AllowedLoginMethods { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ClientAppRedirectUri> RedirectUris { get; set; } = new List<ClientAppRedirectUri>();
}
