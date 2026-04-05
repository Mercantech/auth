namespace Auth.API.Models.Entities;

public class ClientAppRedirectUri
{
    public Guid Id { get; set; }
    public Guid ClientAppId { get; set; }
    public string Uri { get; set; } = string.Empty;

    public ClientApp ClientApp { get; set; } = null!;
}
