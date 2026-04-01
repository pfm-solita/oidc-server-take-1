namespace OidcServer.IntegrationTests.Tests;

[CollectionDefinition(IntegrationTestCollection.Name)]
public class IntegrationTestCollection : ICollectionFixture<OidcServer.IntegrationTests.Infrastructure.PlaywrightFixture>
{
    public const string Name = "Integration";
}
