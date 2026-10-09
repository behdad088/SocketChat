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
    [InlineData("mt_doc_userchanneldocument")]
    [InlineData("mt_doc_conversationdocument")]
    [InlineData("mt_doc_messagedocument")]
    [InlineData("mt_doc_messageversiondocument")]
    public async Task Schema_ContainsDocumentTable(string tableName)
    {
        var tables = await fixture.GetTableNamesAsync();

        tables.ShouldContain(tableName);
    }

    [Fact]
    public async Task Schema_IndexesUserChannelsForTheChatList()
    {
        var definition = await fixture.GetIndexDefinitionAsync("mt_doc_userchanneldocument_idx_chat_list");

        definition.ShouldBe(
            "CREATE INDEX mt_doc_userchanneldocument_idx_chat_list ON public.mt_doc_userchanneldocument " +
            "USING btree (((data ->> 'UserId'::text)), (((data ->> 'IsPinned'::text))::boolean), " +
            "((data ->> 'LastMessageId'::text)))");
    }

    [Fact]
    public async Task Schema_IndexesMessagesByConversationIdAndId()
    {
        var definition = await fixture.GetIndexDefinitionAsync("mt_doc_messagedocument_idx_conversation_id_id");

        definition.ShouldBe(
            "CREATE INDEX mt_doc_messagedocument_idx_conversation_id_id ON public.mt_doc_messagedocument " +
            "USING btree (((data ->> 'ConversationId'::text)), id)");
    }

    [Fact]
    public async Task Schema_IndexesMessagesByConversationIdAndRevisionId()
    {
        var definition = await fixture.GetIndexDefinitionAsync("mt_doc_messagedocument_idx_conversation_id_revision_id");

        definition.ShouldBe(
            "CREATE INDEX mt_doc_messagedocument_idx_conversation_id_revision_id ON public.mt_doc_messagedocument " +
            "USING btree (((data ->> 'ConversationId'::text)), ((data ->> 'RevisionId'::text)))");
    }
}
