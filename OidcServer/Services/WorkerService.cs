using OpenIddict.Abstractions;
using Microsoft.Extensions.Configuration;

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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
