using Chat.IntegrationTests.Infrastructure;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class SchemaTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Schema_AfterApplyingChanges_MatchesTheConfiguration()
    {
        await Should.NotThrowAsync(() => fixture.Store.Storage.Database.AssertDatabaseMatchesConfigurationAsync());
    }

    [Theory]
    [InlineData("mt_doc_profiledocument")]
    public async Task Schema_ContainsDocumentTable(string tableName)
    {
        var tables = await fixture.GetTableNamesAsync();

        tables.ShouldContain(tableName);
    }
}
