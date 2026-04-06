using Auth.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auth.API.Data;

/// <summary>
/// Design-time factory til EF migrations (dotnet-ef).
/// </summary>
public sealed class AuthDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        // Brug env var hvis sat, ellers lokal default til udvikling.
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                 ?? "Host=localhost;Port=5432;Database=mercantec_auth;Username=auth;Password=auth";

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(cs)
            .Options;

        return new AuthDbContext(options);
    }
}

