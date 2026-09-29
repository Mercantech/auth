namespace Auth.API.Options;

/// <summary>Konfiguration til Cursor/MCP admin-adgang via central API-nøgle.</summary>
public class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>
    /// Delt hemmelighed sendt som <c>x-api-key</c>. Tom = API-nøgle-auth er slået fra
    /// (kun Admin-JWT virker stadig).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
