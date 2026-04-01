using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OidcServer.IntegrationTests.Infrastructure;

namespace OidcServer.IntegrationTests.Tests;

[CollectionDefinition(ExternalOidcCollection.Name)]
public class ExternalOidcCollection : ICollectionFixture<ExternalOidcFixture>
{
    public const string Name = "ExternalOidc";
}

/// <summary>
/// Integration tests that exercise the full OpenID Connect authorization-code flow
/// between two real Kestrel instances of the OIDC server:
/// one acting as the external identity provider (IdP) and one as the relying party (RP).
/// </summary>
[Collection(ExternalOidcCollection.Name)]
public class ExternalOidcLoginTests
{
    private const string AdminEmail = "admin@oidcserver.local";
    private const string AdminPassword = "Admin123!";

    private readonly ExternalOidcFixture _fixture;
    private IBrowser Browser => _fixture.Browser;

    public ExternalOidcLoginTests(ExternalOidcFixture fixture)
    {
        _fixture = fixture;
    }

    private string RpUrl(string path) => _fixture.RpBaseUrl.TrimEnd('/') + path;

    [Fact]
    public async Task ExternalOidcLogin_AuthenticatesUserOnRelyingParty()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Step 1: Navigate to the RP login page; the external-provider button must be visible.
        await page.GotoAsync(RpUrl("/account/login"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");
        await Assertions.Expect(page.Locator("a.btn-secondary")).ToBeVisibleAsync();

        // Step 2: Click the external-provider button.
        //         The RP issues a Challenge, which redirects the browser to the IdP's
        //         /connect/authorize endpoint, which in turn sends the browser to the
        //         IdP's login page.
        await page.ClickAsync("a.btn-secondary");

        // Step 3: Should now be on the IdP's login page (different host/port from the RP).
        await page.WaitForURLAsync(new Regex(Regex.Escape(_fixture.IdpBaseUrl) + ".*"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");

        // Step 4: Authenticate at the IdP.
        await page.FillAsync("input#email", AdminEmail);
        await page.FillAsync("input#password", AdminPassword);
        await page.ClickAsync("button.btn");

        // Step 5: The IdP processes the authorization request and redirects the browser
        //         back to the RP's /signin-oidc-external callback.  The RP's OIDC
        //         middleware exchanges the code for tokens (backchannel), signs the user
        //         in, and redirects to the final destination on the RP.
        await page.WaitForURLAsync(new Regex(Regex.Escape(_fixture.RpBaseUrl) + ".*"));

        // Step 6: The user is now authenticated on the RP.
        //         The shared layout shows the user's name and a Logout link.
        await Assertions.Expect(page.Locator("nav")).ToContainTextAsync(AdminEmail);
        await Assertions.Expect(page.Locator("nav")).ToContainTextAsync("Logout");
    }

    [Fact]
    public async Task ExternalOidcLogin_InvalidCredentialsAtIdP_ShowsError()
    {
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // Navigate to RP, then follow the external-provider redirect.
        await page.GotoAsync(RpUrl("/account/login"));
        await page.ClickAsync("a.btn-secondary");

        await page.WaitForURLAsync(new Regex(Regex.Escape(_fixture.IdpBaseUrl) + ".*"));
        await Assertions.Expect(page.Locator("h1")).ToHaveTextAsync("Sign In");

        // Submit bad credentials at the IdP.
        await page.FillAsync("input#email", "nobody@example.com");
        await page.FillAsync("input#password", "wrongpassword");
        await page.ClickAsync("button.btn");

        // Should stay on the IdP's login page and show an error.
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(Regex.Escape(_fixture.IdpBaseUrl) + ".*"));
        await Assertions.Expect(page.Locator(".alert-error")).ToBeVisibleAsync();
    }
}
