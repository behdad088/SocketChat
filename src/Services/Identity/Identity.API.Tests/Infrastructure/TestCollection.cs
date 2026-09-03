namespace Identity.API.Tests.Infrastructure;

[CollectionDefinition(Name)]
public class TestCollection : ICollectionFixture<IdentityApiSpecification>
{
    public const string Name = "Integration";
}
