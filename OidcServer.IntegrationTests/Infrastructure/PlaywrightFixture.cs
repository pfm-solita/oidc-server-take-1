using Microsoft.Playwright;

namespace OidcServer.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection fixture that manages the WebApplicationFactory and a Playwright browser instance.
/// Shared across all tests in the "Integration" collection.
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public IntegrationWebApplicationFactory Factory { get; private set; } = null!;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Base URL of the real Kestrel server (for Playwright).</summary>
    public string BaseUrl => Factory.ServerAddress;

    public async Task InitializeAsync()
    {
        Factory = new IntegrationWebApplicationFactory();
        // Trigger server startup by creating an HttpClient (calls EnsureServer internally)
        Factory.CreateClient();

        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
        Factory.Dispose();
    }
}
