using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Auth.Tests.Integration;

/// <summary>
/// Én PostgreSQL-container og én <see cref="WebApplicationFactory{TEntryPoint}"/> pr. testkollektion.
/// Kræver Docker. Kør kun med <c>dotnet test</c> når Docker kører.
/// </summary>
public sealed class AuthIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();
        await _postgres.StartAsync();

        var connectionString = _postgres.GetConnectionString();
        var jwtDir = Path.Combine(Path.GetTempPath(), "mercantec-auth-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(jwtDir);

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = connectionString,
                        ["Jwt:KeysDirectory"] = jwtDir,
                    });
            });
        });
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
            await Factory.DisposeAsync();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}

