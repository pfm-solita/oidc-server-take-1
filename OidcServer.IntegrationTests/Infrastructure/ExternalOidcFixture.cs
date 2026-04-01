using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OidcServer.Data;
using OidcServer.Models;

namespace OidcServer.IntegrationTests.Infrastructure;

/// <summary>
/// Factory for the IdP role in the two-server external OIDC test.
/// Disables 2FA and relaxes OpenIddict's transport-security requirement so that
/// plain-HTTP Kestrel can serve the authorization and token endpoints.
/// </summary>
public sealed class ExternalOidcIdpFactory : IntegrationWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);  // base sets TwoFactorEnabled = true

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TwoFactorEnabled"] = "false",
            }));

        builder.ConfigureServices(services =>
            services.PostConfigure<OpenIddictServerAspNetCoreOptions>(opts =>
                opts.DisableTransportSecurityRequirement = true));
    }
}

/// <summary>
/// Factory for the RP role in the two-server external OIDC test.
/// Set <see cref="IdpBaseUrl"/> before first use; the fixture does this before
/// calling <c>CreateClient()</c>.
/// </summary>
public sealed class ExternalOidcRpFactory : IntegrationWebApplicationFactory
{
    /// <summary>Base URL of the IdP server (e.g. "http://127.0.0.1:54321").</summary>
    public string IdpBaseUrl { get; set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);  // base sets TwoFactorEnabled = true

        builder.ConfigureAppConfiguration((_, cfg) =>
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TwoFactorEnabled"] = "false",
            }));

        // OIDC scheme options (Authority, ClientId, ClientSecret, RequireHttpsMetadata, …)
        // are read from the ExternalProviders database table by DynamicOidcConfigureOptions,
        // which is registered in Program.cs.  No per-test PostConfigure is needed.
    }
}

/// <summary>
/// xUnit collection fixture that starts two real Kestrel OIDC servers: one acting as
/// the external identity provider (IdP) and one as the relying party (RP).
/// The IdP's "test-rp" client is registered in OpenIddict, and a corresponding
/// <see cref="ExternalProvider"/> row is seeded into the RP's database so that the
/// login page renders the external-provider button.
/// </summary>
public sealed class ExternalOidcFixture : IAsyncLifetime
{
    public const string RpClientId = "test-rp";
    public const string RpClientSecret = "test-rp-secret";

    private ExternalOidcIdpFactory _idpFactory = null!;
    private ExternalOidcRpFactory _rpFactory = null!;

    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>Base URL of the IdP Kestrel server.</summary>
    public string IdpBaseUrl { get; private set; } = string.Empty;

    /// <summary>Base URL of the RP Kestrel server.</summary>
    public string RpBaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // 1. Start the IdP server first so we know its URL.
        _idpFactory = new ExternalOidcIdpFactory();
        _idpFactory.CreateClient();
        IdpBaseUrl = _idpFactory.ServerAddress;

        // 2. Start the RP server, configured to delegate to the IdP.
        _rpFactory = new ExternalOidcRpFactory { IdpBaseUrl = IdpBaseUrl };
        _rpFactory.CreateClient();
        RpBaseUrl = _rpFactory.ServerAddress;

        // 3. Register the RP as a confidential client on the IdP so that the
        //    authorization code flow succeeds.
        var rpCallbackUrl = $"{RpBaseUrl}/signin-oidc-external";
        await using var idpScope = _idpFactory.Services.CreateAsyncScope();
        var appManager = idpScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = RpClientId,
            ClientSecret = RpClientSecret,
            DisplayName = "Test RP Client",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.Email,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
            },
            RedirectUris = { new Uri(rpCallbackUrl) },
        });

        // 4. Seed the ExternalProvider row in the RP's database so the login page
        //    renders the external-provider button that links to the IdP.
        await using var rpScope = _rpFactory.Services.CreateAsyncScope();
        var db = rpScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ExternalProviders.Add(new ExternalProvider
        {
            Name = "oidc-external",
            DisplayName = "External OIDC Provider",
            Type = "oidc",
            Authority = IdpBaseUrl,
            ClientId = RpClientId,
            ClientSecret = RpClientSecret,
            Enabled = true,
        });
        await db.SaveChangesAsync();

        // 5. Start Playwright.
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
        _rpFactory.Dispose();
        _idpFactory.Dispose();
    }
}
