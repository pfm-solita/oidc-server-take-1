using Microsoft.Playwright;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public class LoginFlowTests : IntegrationTestBase
{
    private const string AdminEmail = "admin@oidcserver.local";
    private const string AdminPassword = "Admin123!";

    public LoginFlowTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task FullLoginFlow_WithTwoFactor_Succeeds()
    {
        FakeEmail.Clear();

        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Step 1: Navigate to login page
        await page.GotoAsync(Url("/account/login"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");

        // Step 2: Fill credentials and submit
        await page.FillAsync("input#email", AdminEmail);
        await page.FillAsync("input#password", AdminPassword);
        await page.ClickAsync("button.btn");

        // Step 3: Should redirect to 2FA page
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/account/two-factor"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Two-Factor Authentication");

        // Step 4: Poll for the 2FA code captured by the fake email service
        string? code = null;
        for (var i = 0; i < 20; i++)
        {
            code = FakeEmail.GetTwoFactorCode(AdminEmail);
            if (code != null) break;
            await Task.Delay(100);
        }
        Assert.NotNull(code);
        Assert.Equal(6, code!.Length);
        Assert.True(code.All(char.IsDigit), "2FA code should be numeric");

        // Step 5: Enter the 2FA code
        await page.FillAsync("input#code", code);
        await page.ClickAsync("button.btn");

        // Step 6: Should be redirected away from the 2FA page after successful login
        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("^(?!.*/account/two-factor).*$"));

        // Step 7: Verify we are logged in by navigating to the admin dashboard
        // The admin user has the Admin role, so /admin is accessible when authenticated
        await page.GotoAsync(Url("/admin"));
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/admin$"));
        await Assertions.Expect(page.Locator("nav")).ToContainTextAsync(AdminEmail);
    }

    [Fact]
    public async Task LoginFlow_WithWrongTwoFactorCode_ShowsError()
    {
        FakeEmail.Clear();

        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/login"));
        await page.FillAsync("input#email", AdminEmail);
        await page.FillAsync("input#password", AdminPassword);
        await page.ClickAsync("button.btn");

        await page.WaitForURLAsync(new System.Text.RegularExpressions.Regex("/account/two-factor"));

        // Enter a wrong code
        await page.FillAsync("input#code", "000000");
        await page.ClickAsync("button.btn");

        // Should stay on 2FA page and show an error
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/account/two-factor"));
        await Assertions.Expect(page.Locator(".alert-error")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Registration_NewUser_ShowsConfirmationPage()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        var uniqueEmail = $"testuser_{Guid.NewGuid():N}@example.com";

        await page.GotoAsync(Url("/account/register"));
        await page.FillAsync("input#displayName", "Test User");
        await page.FillAsync("input#email", uniqueEmail);
        await page.FillAsync("input#password", "Password123!");
        await page.FillAsync("input#confirmPassword", "Password123!");
        await page.ClickAsync("button.btn");

        // RegisterConfirmation view shows "Check Your Email"
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Check Your Email");
    }

    [Fact]
    public async Task Registration_PasswordMismatch_ShowsError()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/register"));
        await page.FillAsync("input#email", "mismatch@example.com");
        await page.FillAsync("input#password", "Password123!");
        await page.FillAsync("input#confirmPassword", "DifferentPassword!");
        await page.ClickAsync("button.btn");

        // Should stay on register page with a validation error
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/account/register"));
        await Assertions.Expect(page.Locator(".alert-error")).ToBeVisibleAsync();
    }
}
