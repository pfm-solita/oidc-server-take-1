using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Playwright;

namespace OidcServer.IntegrationTests.Infrastructure;

/// <summary>
/// xUnit collection fixture like <see cref="PlaywrightFixture"/> but starts the server
/// with <c>Auth:TwoFactorEnabled=false</c> so that the login-without-2FA flow can be tested.
/// </summary>
public sealed class PlaywrightFixtureNoTwoFactor : IAsyncLifetime
{
    public IntegrationWebApplicationFactoryNoTwoFactor Factory { get; private set; } = null!;
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public string BaseUrl => Factory.ServerAddress;

    public async Task InitializeAsync()
    {
        Factory = new IntegrationWebApplicationFactoryNoTwoFactor();
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

/// <summary>
/// A variant of <see cref="IntegrationWebApplicationFactory"/> that explicitly disables 2FA
/// so the login-without-2FA integration tests can run against a real server.
/// </summary>
public sealed class IntegrationWebApplicationFactoryNoTwoFactor : IntegrationWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Override the 2FA flag back to false after the base class sets it to true.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TwoFactorEnabled"] = "false"
            });
        });
    }
}
