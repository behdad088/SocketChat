using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;
using Marten;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class MessageDocumentTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Insert_ThenLoad_ReturnsEveryField()
    {
        var message = NewMessage(Ulid.NewUlid().ToString()) with
        {
            RevisionId = Ulid.NewUlid().ToString(),
            Content = "multi-line\nand \"quoted\" text",
            RepliedTo = Ulid.NewUlid().ToString(),
            Reactions =
            [
                new UserReaction
                {
                    UserId = Guid.NewGuid().ToString(),
                    Emoji = "👍",
                    CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 1, 0, TimeSpan.Zero)
                }
            ],
            UpdatedAt = new DateTimeOffset(2026, 10, 7, 9, 2, 0, TimeSpan.Zero),
            IsEdited = true,
            Version = 2
        };

        await InsertAsync(message);

        await using var query = fixture.Store.QuerySession();
        var loaded = await query.LoadAsync<MessageDocument>(message.Id);

        loaded.ShouldNotBeNull();
        loaded.Reactions.ShouldBe(message.Reactions);
        (loaded with { Reactions = null }).ShouldBe(message with { Reactions = null });
    }

    [Fact]
    public async Task Insert_SoftDeleted_KeepsTheRowWithEmptyContent()
    {
        var message = NewMessage(Ulid.NewUlid().ToString()) with
        {
            Content = string.Empty,
            IsDeleted = true,
            DeletedAt = new DateTimeOffset(2026, 10, 7, 9, 5, 0, TimeSpan.Zero)
        };

        await InsertAsync(message);

        await using var query = fixture.Store.QuerySession();
        var loaded = await query.LoadAsync<MessageDocument>(message.Id);

        loaded.ShouldNotBeNull();
        loaded.IsDeleted.ShouldBeTrue();
        loaded.Content.ShouldBeEmpty();
        loaded.DeletedAt.ShouldBe(message.DeletedAt);
    }

    [Fact]
    public async Task Insert_StoresJsonWithSchemaFieldNames()
    {
        var message = NewMessage(Ulid.NewUlid().ToString()) with
        {
            Reactions =
            [
                new UserReaction
                {
                    UserId = Guid.NewGuid().ToString(),
                    Emoji = "🎉",
                    CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 1, 0, TimeSpan.Zero)
                }
            ]
        };

        await InsertAsync(message);

        var keys = await fixture.GetJsonKeysAsync("mt_doc_messagedocument", message.Id);
        var reactionKeys = await fixture.GetFirstElementJsonKeysAsync("mt_doc_messagedocument", message.Id, "Reactions");

        keys.ShouldBe(
        [
            "Id", "RevisionId", "ConversationId", "SenderId", "Content", "RepliedTo", "Reactions",
            "CreatedAt", "UpdatedAt", "IsEdited", "IsDeleted", "DeletedAt", "Version"
        ], ignoreOrder: true);
        reactionKeys.ShouldBe(["UserId", "Emoji", "CreatedAt"], ignoreOrder: true);
    }

    [Fact]
    public async Task OrderingByIdDescending_ReturnsMessagesNewestFirst()
    {
        var conversationId = Ulid.NewUlid().ToString();
        var sentAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
        var idsOldestFirst = Enumerable.Range(0, 50)
            .Select(i => Ulid.NewUlid(sentAt.AddMilliseconds(i * 37)).ToString())
            .ToList();

        await using (var session = fixture.Store.LightweightSession())
        {
            // Insert out of order, the way competing consumers may persist them.
            foreach (var id in idsOldestFirst.OrderBy(_ => Random.Shared.Next()))
                session.Insert(NewMessage(id) with { ConversationId = conversationId });
            await session.SaveChangesAsync();
        }

        await using var query = fixture.Store.QuerySession();
        var newestFirst = await query.Query<MessageDocument>()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync();

        newestFirst.ShouldBe(Enumerable.Reverse(idsOldestFirst).ToList());
    }

    [Fact]
    public async Task KeysetPage_UsesTheConversationIdIdIndex()
    {
        await using var query = fixture.Store.QuerySession();
        var command = query.Query<MessageDocument>()
            .Where(message => message.ConversationId == "01KX6WMD905AN68KKFWQVDNCHZ"
                              && message.Id.CompareTo("01KX6Y4MJQ9512TXYZB2CTHGP5") < 0)
            .OrderByDescending(message => message.Id)
            .Take(50)
            .ToCommand();

        var plan = await fixture.ExplainWithoutSeqScanAsync(command);

        plan.ShouldContain("mt_doc_messagedocument_idx_conversation_id_id");
    }

    [Fact]
    public async Task ChangesSinceCursors_UsesTheConversationIdRevisionIdIndex()
    {
        await using var query = fixture.Store.QuerySession();
        var command = query.Query<MessageDocument>()
            .Where(message => message.ConversationId == "01KX6WMD905AN68KKFWQVDNCHZ"
                              && (message.Id.CompareTo("01KX6Y4MJQ9512TXYZB2CTHGP5") > 0
                                  || message.RevisionId!.CompareTo("01M1NRXF1QFMZG23FM7A0B74T7") > 0))
            .ToCommand();

        var plan = await fixture.ExplainWithoutSeqScanAsync(command);

        plan.ShouldContain("mt_doc_messagedocument_idx_conversation_id_revision_id");
    }

    private static MessageDocument NewMessage(string id) => new()
    {
        Id = id,
        ConversationId = "01KX6WMD905AN68KKFWQVDNCHZ",
        SenderId = Guid.NewGuid().ToString(),
        Content = "hello",
        CreatedAt = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)
    };

    private async Task InsertAsync(MessageDocument message)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Insert(message);
        await session.SaveChangesAsync();
    }
}
