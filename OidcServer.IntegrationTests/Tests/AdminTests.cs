using Microsoft.Playwright;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[Collection("Integration")]
public class AdminTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@oidcserver.local";
    private const string AdminPassword = "Admin123!";

    public AdminTests(PlaywrightFixture fixture) : base(fixture) { }

    /// <summary>Performs the full login flow (credentials + 2FA) for the admin user.</summary>
    private async Task LoginAsAdminAsync(IPage page)
    {
        FakeEmail.Clear();

        await page.GotoAsync(Url("/account/login"));
        await page.FillAsync("input#email", AdminEmail);
        await page.FillAsync("input#password", AdminPassword);
        await page.ClickAsync("button.btn");

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/account/two-factor"));

        string? code = null;
        for (var i = 0; i < 20; i++)
        {
            code = FakeEmail.GetTwoFactorCode(AdminEmail);
            if (code != null) break;
            await Task.Delay(100);
        }
        Assert.NotNull(code);

        await page.FillAsync("input#code", code!);
        await page.ClickAsync("button.btn");

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("^(?!.*/account/two-factor).*$"));
    }

    [Fact]
    public async Task AdminDashboard_IsAccessible_WhenLoggedInAsAdmin()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await LoginAsAdminAsync(page);

        await page.GotoAsync(Url("/admin"));

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Admin Dashboard");
        await Assertions.Expect(page.Locator("a[href='/admin/clients']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("a[href='/admin/scopes']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("a[href='/admin/external-providers']")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("a[href='/admin/users']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminDashboard_RedirectsToLogin_WhenNotLoggedIn()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/admin"));

        // Should redirect to login page
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/account/login"));
    }

    [Fact]
    public async Task AdminClientsPage_ShowsOAuthClients()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await LoginAsAdminAsync(page);

        await page.GotoAsync(Url("/admin/clients"));

        // The clients page has <h1>OAuth Clients</h1>
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("OAuth Clients");
        // The admin-cli client seeded by WorkerService should appear
        await Assertions.Expect(page.Locator("body")).ToContainTextAsync("admin-cli");
    }

    [Fact]
    public async Task AdminScopesPage_IsAccessible()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await LoginAsAdminAsync(page);

        await page.GotoAsync(Url("/admin/scopes"));

        // The scopes page has <h1>OAuth Scopes</h1>
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("OAuth Scopes");
    }

    [Fact]
    public async Task AdminUsersPage_ShowsAdminUser()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await LoginAsAdminAsync(page);

        await page.GotoAsync(Url("/admin/users"));

        await Assertions.Expect(page.Locator("body")).ToContainTextAsync(AdminEmail);
    }
}
