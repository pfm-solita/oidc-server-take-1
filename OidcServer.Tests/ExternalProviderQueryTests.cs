using Microsoft.EntityFrameworkCore;
using OidcServer.Data;
using OidcServer.Models;

namespace OidcServer.Tests;

public class ExternalProviderQueryTests
{
    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ExternalProviders_OnlyEnabledProviders_AreReturned()
    {
        using var db = CreateDb();
        db.ExternalProviders.AddRange(
            new ExternalProvider { Name = "provider-a", DisplayName = "Provider A", Type = "oidc", Enabled = true },
            new ExternalProvider { Name = "provider-b", DisplayName = "Provider B", Type = "oidc", Enabled = false });
        await db.SaveChangesAsync();

        var result = await db.ExternalProviders.Where(p => p.Enabled).ToListAsync();

        Assert.Single(result);
        Assert.Equal("provider-a", result[0].Name);
    }

    [Fact]
    public async Task ExternalProviders_AreReturnedOrderedByDisplayName()
    {
        using var db = CreateDb();
        db.ExternalProviders.AddRange(
            new ExternalProvider { Name = "provider-z", DisplayName = "Zeta", Type = "oidc", Enabled = true },
            new ExternalProvider { Name = "provider-a", DisplayName = "Alpha", Type = "oidc", Enabled = true });
        await db.SaveChangesAsync();

        var result = await db.ExternalProviders
            .Where(p => p.Enabled)
            .OrderBy(p => p.DisplayName)
            .ToListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].DisplayName);
        Assert.Equal("Zeta", result[1].DisplayName);
    }

    [Fact]
    public async Task ExternalProviders_WhenNoneEnabled_ReturnsEmpty()
    {
        using var db = CreateDb();
        db.ExternalProviders.AddRange(
            new ExternalProvider { Name = "provider-a", DisplayName = "Provider A", Type = "oidc", Enabled = false });
        await db.SaveChangesAsync();

        var result = await db.ExternalProviders.Where(p => p.Enabled).ToListAsync();

        Assert.Empty(result);
    }
}
