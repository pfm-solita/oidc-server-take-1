using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Microsoft.Extensions.Configuration;
using OidcServer.Data;

namespace OidcServer.Services;

public class WorkerService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public WorkerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (await manager.FindByClientIdAsync("admin-cli", cancellationToken) is null)
        {
            var clientSecret = config["AdminCli:ClientSecret"] ?? "admin-cli-secret";
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "admin-cli",
                ClientSecret = clientSecret,
                DisplayName = "Admin CLI Client",
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                }
            }, cancellationToken);
        }

        // Pre-register OpenIdConnect schemes for any OIDC external providers that are
        // already in the database.  This means a provider configured before the app
        // starts is immediately available without waiting for the first login request.
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        var oidcProviders = await db.ExternalProviders
            .Where(p => p.Type == "oidc" && p.Enabled)
            .ToListAsync(cancellationToken);

        foreach (var provider in oidcProviders)
        {
            if (await schemeProvider.GetSchemeAsync(provider.Name) is null)
            {
                schemeProvider.AddScheme(new AuthenticationScheme(
                    provider.Name,
                    provider.DisplayName,
                    typeof(OpenIdConnectHandler)));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
