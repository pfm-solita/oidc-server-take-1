using Microsoft.Playwright;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public class AccountPageTests : IntegrationTestBase
{
    public AccountPageTests(PlaywrightFixture fixture) : base(fixture) { }

    [Fact]
    public async Task LoginPage_RendersCorrectly()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/login"));

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");
        await Assertions.Expect(page.Locator("input#email")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input#password")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button.btn")).ToBeVisibleAsync();
        // Scope to the card to avoid matching the navbar's "Register" link
        await Assertions.Expect(page.Locator(".card a[href='/account/register']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RegisterPage_RendersCorrectly()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/register"));

        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Create Account");
        await Assertions.Expect(page.Locator("input#displayName")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input#email")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input#password")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input#confirmPassword")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("button.btn")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RegisterPage_HasLinkToLogin()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/register"));

        // Scope to the card to avoid matching the navbar's "Login" link
        await Assertions.Expect(page.Locator(".card a[href='/account/login']")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task LoginPage_HasNavbarBranding()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/login"));

        await Assertions.Expect(page.Locator("nav")).ToContainTextAsync("OIDC Server");
    }

    [Fact]
    public async Task LoginPage_InvalidCredentials_ShowsError()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(Url("/account/login"));
        await page.FillAsync("input#email", "nonexistent@example.com");
        await page.FillAsync("input#password", "wrongpassword");
        await page.ClickAsync("button.btn");

        // Should stay on login page with an error message
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/account/login"));
        await Assertions.Expect(page.Locator(".alert-error")).ToBeVisibleAsync();
    }
}
