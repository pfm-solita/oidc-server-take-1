using System.Net;
using System.Text.Json;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public class OidcEndpointTests : IntegrationTestBase
{
    private readonly HttpClient _httpClient;

    public OidcEndpointTests(PlaywrightFixture fixture) : base(fixture)
    {
        // Use https://localhost so OpenIddict's transport security check treats requests as HTTPS.
        // The TestServer's in-memory handler honours the scheme from the base address.
        _httpClient = fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task OidcDiscovery_ReturnsValidDocument()
    {
        var response = await _httpClient.GetAsync("/.well-known/openid-configuration");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("issuer", out _), "Missing 'issuer'");
        Assert.True(doc.RootElement.TryGetProperty("authorization_endpoint", out _), "Missing 'authorization_endpoint'");
        Assert.True(doc.RootElement.TryGetProperty("token_endpoint", out _), "Missing 'token_endpoint'");
        Assert.True(doc.RootElement.TryGetProperty("userinfo_endpoint", out _), "Missing 'userinfo_endpoint'");
        Assert.True(doc.RootElement.TryGetProperty("jwks_uri", out _), "Missing 'jwks_uri'");
    }

    [Fact]
    public async Task OidcDiscovery_IncludesOpenIdScope()
    {
        var response = await _httpClient.GetAsync("/.well-known/openid-configuration");
        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        if (doc.RootElement.TryGetProperty("scopes_supported", out var scopes))
        {
            var scopeList = scopes.EnumerateArray().Select(s => s.GetString()).ToList();
            Assert.Contains("openid", scopeList);
        }
    }

    [Fact]
    public async Task TokenEndpoint_ClientCredentials_ReturnsToken()
    {
        // The admin-cli client is seeded by WorkerService at startup
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "admin-cli",
            ["client_secret"] = "admin-cli-secret",
            ["scope"] = "openid",
        });

        var response = await _httpClient.PostAsync("/connect/token", formContent);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(content);

        Assert.True(doc.RootElement.TryGetProperty("access_token", out _), "Missing 'access_token'");
        Assert.True(doc.RootElement.TryGetProperty("token_type", out _), "Missing 'token_type'");
    }

    [Fact]
    public async Task LoginPage_ReturnsOk()
    {
        var response = await _httpClient.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegisterPage_ReturnsOk()
    {
        var response = await _httpClient.GetAsync("/account/register");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminPage_RequiresAuth_RedirectsToLogin()
    {
        var response = await _httpClient.GetAsync("/admin");

        // Should return 302 redirect toward the login page
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/account/login", response.Headers.Location?.ToString() ?? "");
    }
}
