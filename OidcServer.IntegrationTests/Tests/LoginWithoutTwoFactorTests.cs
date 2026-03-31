using Microsoft.Playwright;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[CollectionDefinition("IntegrationNoTwoFactor")]
public class IntegrationNoTwoFactorCollection
    : ICollectionFixture<PlaywrightFixtureNoTwoFactor> { }

[Collection("IntegrationNoTwoFactor")]
public class LoginWithoutTwoFactorTests
{
    private const string AdminEmail = "admin@oidcserver.local";
    private const string AdminPassword = "Admin123!";

    private readonly PlaywrightFixtureNoTwoFactor _fixture;
    private IBrowser Browser => _fixture.Browser;
    private string BaseUrl => _fixture.BaseUrl;

    public LoginWithoutTwoFactorTests(PlaywrightFixtureNoTwoFactor fixture)
    {
        _fixture = fixture;
    }

    private string Url(string path) => BaseUrl.TrimEnd('/') + path;

    [Fact]
    public async Task FullLoginFlow_WithoutTwoFactor_SignsInDirectly()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Step 1: Navigate to login page
        await page.GotoAsync(Url("/account/login"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");

        // Step 2: Submit credentials
        await page.FillAsync("input#email", AdminEmail);
        await page.FillAsync("input#password", AdminPassword);
        await page.ClickAsync("button.btn");

        // Step 3: Should NOT be redirected to the 2FA page; verify by navigating to admin
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("^(?!.*/account/two-factor).*$"));
        await page.GotoAsync(Url("/admin"));
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin$"));
        await Assertions.Expect(page.Locator("nav")).ToContainTextAsync(AdminEmail);
    }

    [Fact]
    public async Task LoginFlow_WithoutTwoFactor_InvalidCredentials_ShowsError()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/login"));
        await page.FillAsync("input#email", "noone@example.com");
        await page.FillAsync("input#password", "wrongpassword");
        await page.ClickAsync("button.btn");

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/account/login"));
        await Assertions.Expect(page.Locator(".alert-error")).ToBeVisibleAsync();
    }
}
