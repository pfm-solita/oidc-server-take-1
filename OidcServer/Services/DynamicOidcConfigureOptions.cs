using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using OidcServer.Data;

namespace OidcServer.Services;

/// <summary>
/// Reads OIDC provider configuration from the database and applies it as named
/// <see cref="OpenIdConnectOptions"/> so that dynamically registered schemes
/// receive the correct authority, client credentials, and other settings at runtime.
///
/// This allows an admin to create/manage external OIDC providers through the UI
/// without requiring an application restart to update hard-coded configuration.
/// </summary>
public sealed class DynamicOidcConfigureOptions : IConfigureNamedOptions<OpenIdConnectOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public DynamicOidcConfigureOptions(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (string.IsNullOrEmpty(name))
            return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var provider = db.ExternalProviders
            .FirstOrDefault(p => p.Name == name && p.Type == "oidc" && p.Enabled);

        if (provider is null)
            return;

        options.Authority = provider.Authority;
        options.ClientId = provider.ClientId;
        options.ClientSecret = provider.ClientSecret;
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.CallbackPath = "/signin-oidc-external";
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = true;

        if (!options.Scope.Contains("email"))
            options.Scope.Add("email");
        if (!options.Scope.Contains("profile"))
            options.Scope.Add("profile");

        // Allow plain-HTTP authorities (e.g. local/development IdPs).
        if (provider.Authority?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true)
            options.RequireHttpsMetadata = false;
    }

    // Called for the default (unnamed) options instance — not used here.
    public void Configure(OpenIdConnectOptions options) =>
        Configure(Options.DefaultName, options);
}
