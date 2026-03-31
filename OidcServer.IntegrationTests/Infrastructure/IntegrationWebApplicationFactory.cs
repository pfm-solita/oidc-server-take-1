using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OidcServer.Data;
using OidcServer.Services;
using System.Net;

namespace OidcServer.IntegrationTests.Infrastructure;

public class IntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private IHost? _realHost;
    private string _serverAddress = "http://localhost:0";
    private readonly string _dbPath;
    private readonly FakeEmailService _fakeEmail = new();

    public IntegrationWebApplicationFactory()
    {
        _dbPath = Path.Combine(AppContext.BaseDirectory, $"oidctest_{Guid.NewGuid():N}.db");
    }

    /// <summary>The base URL of the real Kestrel server (for Playwright).</summary>
    public string ServerAddress => _serverAddress;

    /// <summary>The fake email service instance (shared with the real host).</summary>
    public FakeEmailService FakeEmail => _fakeEmail;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Enable 2FA so the existing 2FA integration tests continue to pass.
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:TwoFactorEnabled"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_dbPath}");
                options.UseOpenIddict();
            });

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(_fakeEmail);
        });
    }

    /// <summary>
    /// Override CreateHost to also start a real Kestrel server on a random port.
    /// Playwright needs a real TCP socket; the in-process WebApplicationFactory server
    /// cannot be used directly by a browser.
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder =>
        {
            webHostBuilder.UseKestrel(options =>
            {
                options.Listen(IPAddress.Loopback, 0);
            });
        });

        _realHost = builder.Build();
        _realHost.Start();

        var server = _realHost.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        _serverAddress = addressFeature!.Addresses.First()
            .Replace("[::]", "localhost")
            .Replace("0.0.0.0", "localhost");

        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _realHost?.Dispose();
            // Clear the SQLite connection pool so the DB file can be deleted.
            SqliteConnection.ClearAllPools();
            try { File.Delete(_dbPath); } catch { /* best effort */ }
            try { File.Delete(_dbPath + "-wal"); } catch { }
            try { File.Delete(_dbPath + "-shm"); } catch { }
        }
    }
}
