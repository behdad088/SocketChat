using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;
using JasperFx;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class MessageVersionDocumentTests(PostgresFixture fixture)
{
    [Fact]
    public void CreateId_JoinsMessageIdAndVersionWithAColon()
    {
        MessageVersionDocument.CreateId("01KX6Y4MJQ9512TXYZB2CTHGP5", 1)
            .ShouldBe("01KX6Y4MJQ9512TXYZB2CTHGP5:1");
    }

    [Fact]
    public async Task Insert_ThenLoad_ReturnsEveryField()
    {
        var snapshot = NewSnapshot(Ulid.NewUlid().ToString(), version: 1) with
        {
            RepliedTo = Ulid.NewUlid().ToString(),
            UpdatedAt = new DateTimeOffset(2026, 10, 7, 9, 2, 0, TimeSpan.Zero),
            IsEdited = true
        };

        await InsertAsync(snapshot);

        await using var query = fixture.Store.QuerySession();
        var loaded = await query.LoadAsync<MessageVersionDocument>(snapshot.Id);

        loaded.ShouldBe(snapshot);
    }

    [Fact]
    public async Task Insert_StoresJsonWithSchemaFieldNames()
    {
        var snapshot = NewSnapshot(Ulid.NewUlid().ToString(), version: 1);

        await InsertAsync(snapshot);

        var keys = await fixture.GetJsonKeysAsync("mt_doc_messageversiondocument", snapshot.Id);

        keys.ShouldBe(
        [
            "Id", "ConversationId", "SenderId", "Content", "RepliedTo", "CreatedAt", "UpdatedAt",
            "IsEdited", "Version"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Insert_SameMessageAndVersionTwice_IsRejected()
    {
        var messageId = Ulid.NewUlid().ToString();
        await InsertAsync(NewSnapshot(messageId, version: 1));

        await Should.ThrowAsync<DocumentAlreadyExistsException>(
            () => InsertAsync(NewSnapshot(messageId, version: 1) with { Content = "edited elsewhere" }));
    }

    private static MessageVersionDocument NewSnapshot(string messageId, int version) => new()
    {
        Id = MessageVersionDocument.CreateId(messageId, version),
        ConversationId = "01KX6WMD905AN68KKFWQVDNCHZ",
        SenderId = Guid.NewGuid().ToString(),
        Content = "first draft",
        CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero),
        Version = version
    };

    private async Task InsertAsync(MessageVersionDocument snapshot)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Insert(snapshot);
        await session.SaveChangesAsync();
    }
}
