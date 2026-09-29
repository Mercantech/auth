using Auth.API.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Auth.API.Security;

/// <summary>Autorisationspolitikker til /api/admin/* (Admin-JWT eller MCP API-nøgle).</summary>
public static class AdminApiPolicies
{
    public const string Name = "AdminApi";

    /// <summary>Kommasepareret liste til <c>[Authorize(AuthenticationSchemes = …)]</c>.</summary>
    public const string AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme + "," + MercantecAuthSchemes.McpApiKey;
}
