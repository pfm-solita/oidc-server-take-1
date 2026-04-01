using Microsoft.Playwright;

namespace OidcServer.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for integration tests that use Playwright and the shared PlaywrightFixture.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly PlaywrightFixture Fixture;
    protected IBrowser Browser => Fixture.Browser;
    protected string BaseUrl => Fixture.BaseUrl;
    protected FakeEmailService FakeEmail => Fixture.Factory.FakeEmail;

    protected IntegrationTestBase(PlaywrightFixture fixture)
    {
        Fixture = fixture;
    }

    /// <summary>Constructs an absolute URL for the given path on the test server.</summary>
    protected string Url(string path) => BaseUrl.TrimEnd('/') + path;
}
