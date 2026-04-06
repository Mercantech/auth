using System.Net;
using System.Text.Json;

namespace Auth.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(nameof(AuthIntegrationCollection))]
public class PublicEndpointTests(AuthIntegrationFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task Health_returns_ok_json()
    {
        var res = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var json = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Jwks_returns_rsa_key_set()
    {
        var res = await _client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var keys = doc.RootElement.GetProperty("keys");
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.True(keys.GetArrayLength() >= 1);
        var k0 = keys[0];
        Assert.Equal("RSA", k0.GetProperty("kty").GetString());
        Assert.Equal("RS256", k0.GetProperty("alg").GetString());
        Assert.True(k0.TryGetProperty("n", out var n) && n.GetString()?.Length > 10);
    }

    [Fact]
    public async Task Integration_manifest_contains_expected_metadata()
    {
        var res = await _client.GetAsync("/.well-known/mercantec-auth.json");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("1.0", root.GetProperty("schema_version").GetString());
        Assert.Equal("https://test.auth.local", root.GetProperty("issuer").GetString());
        Assert.True(root.GetProperty("email_password_login_enabled").GetBoolean());
    }
}
