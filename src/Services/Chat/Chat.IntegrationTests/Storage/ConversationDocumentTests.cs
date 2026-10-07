using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class ConversationDocumentTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Insert_ThenLoad_ReturnsEveryField()
    {
        var conversation = new ConversationDocument
        {
            Id = Ulid.NewUlid().ToString(),
            Participants = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()],
            CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero),
            LastMessageId = "01KX6Y4MJQ9512TXYZB2CTHGP5",
            LastMessageAt = new DateTimeOffset(2026, 10, 7, 9, 15, 0, TimeSpan.Zero)
        };

        await InsertAsync(conversation);

        await using var query = fixture.Store.QuerySession();
        var loaded = await query.LoadAsync<ConversationDocument>(conversation.Id);

        loaded.ShouldNotBeNull();
        loaded.Participants.ShouldBe(conversation.Participants);
        loaded.CreatedAt.ShouldBe(conversation.CreatedAt);
        loaded.LastMessageId.ShouldBe(conversation.LastMessageId);
        loaded.LastMessageAt.ShouldBe(conversation.LastMessageAt);
    }

    [Fact]
    public async Task Insert_StoresJsonWithSchemaFieldNames()
    {
        var conversation = new ConversationDocument
        {
            Id = Ulid.NewUlid().ToString(),
            Participants = [Guid.NewGuid().ToString(), Guid.NewGuid().ToString()],
            CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)
        };

        await InsertAsync(conversation);

        var keys = await fixture.GetJsonKeysAsync("mt_doc_conversationdocument", conversation.Id);

        keys.ShouldBe(["Id", "Participants", "CreatedAt", "LastMessageId", "LastMessageAt"], ignoreOrder: true);
    }

    private async Task InsertAsync(ConversationDocument conversation)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Insert(conversation);
        await session.SaveChangesAsync();
    }
}
