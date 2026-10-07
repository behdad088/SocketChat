namespace Chat.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class StorageCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Storage";
}
