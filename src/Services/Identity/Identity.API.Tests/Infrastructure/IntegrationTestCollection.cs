namespace Identity.API.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class IntegrationTestCollection : ICollectionFixture<IdentityApiSpecification>
{
    public const string Name = "Integration";
}
