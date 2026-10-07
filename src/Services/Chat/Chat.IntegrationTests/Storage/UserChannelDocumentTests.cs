using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;
using JasperFx;
using Marten;
using Marten.Patching;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class UserChannelDocumentTests(PostgresFixture fixture)
{
    [Fact]
    public void CreateId_JoinsUserIdAndPeerUserIdWithAColon()
    {
        var id = UserChannelDocument.CreateId(
            "d9360022-d706-4670-bf05-6c7e0a043732",
            "3d7babc6-b1ed-485c-b598-82d607ee7b4d");

        id.ShouldBe("d9360022-d706-4670-bf05-6c7e0a043732:3d7babc6-b1ed-485c-b598-82d607ee7b4d");
    }

    [Fact]
    public void CreateId_IsDifferentForEachParticipant()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();

        UserChannelDocument.CreateId(userId, peerUserId)
            .ShouldNotBe(UserChannelDocument.CreateId(peerUserId, userId));
    }

    [Fact]
    public async Task Insert_ThenLoad_ReturnsEveryField()
    {
        var channel = NewChannel() with
        {
            State = ChannelState.Muted,
            IsPinned = true,
            LastReadMessageId = "01KX6Y4MJQ9512TXYZB2CTHGP5",
            LastMessageId = "01KX6Y4MJQ9512TXYZB2CTHGP6",
            LastMessageAt = new DateTimeOffset(2026, 10, 7, 9, 15, 0, TimeSpan.Zero),
            LatestMessageBody = "see you tomorrow",
            TotalUnreadMessages = 2
        };

        await InsertAsync(channel);

        // Marten assigns Version: the first write is revision 1.
        (await LoadAsync(channel.Id)).ShouldBe(channel with { Version = 1 });
    }

    [Fact]
    public async Task Insert_WithOnlyRequiredFields_UsesSchemaDefaults()
    {
        var channel = NewChannel();

        await InsertAsync(channel);

        var loaded = await LoadAsync(channel.Id);
        loaded.ShouldNotBeNull();
        loaded.State.ShouldBeNull();
        loaded.IsPinned.ShouldBeFalse();
        loaded.IsPrivate.ShouldBeFalse();
        loaded.LastReadMessageId.ShouldBeNull();
        loaded.LastMessageId.ShouldBeNull();
        loaded.LastMessageAt.ShouldBeNull();
        loaded.LatestMessageBody.ShouldBeNull();
        loaded.TotalUnreadMessages.ShouldBe(0);
        loaded.Version.ShouldBe(1);
    }

    [Fact]
    public async Task Insert_StoresStateAsItsName()
    {
        var channel = NewChannel() with { State = ChannelState.Archived };

        await InsertAsync(channel);

        (await fixture.GetJsonFieldAsync("mt_doc_userchanneldocument", channel.Id, "State")).ShouldBe("Archived");
    }

    [Fact]
    public async Task Insert_StoresJsonWithSchemaFieldNames()
    {
        var channel = NewChannel();

        await InsertAsync(channel);

        var keys = await fixture.GetJsonKeysAsync("mt_doc_userchanneldocument", channel.Id);
        keys.ShouldBe(
        [
            "Id", "ConversationId", "UserId", "State", "IsPinned", "IsPrivate", "LastReadMessageId",
            "LastMessageId", "LastMessageAt", "LatestMessageBody", "TotalUnreadMessages", "Version"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task EveryWrite_IncrementsVersion()
    {
        var channel = NewChannel();
        await InsertAsync(channel);

        await using (var session = fixture.Store.LightweightSession())
        {
            session.Store(channel with { IsPinned = true });
            await session.SaveChangesAsync();
        }

        await using (var session = fixture.Store.LightweightSession())
        {
            session.Patch<UserChannelDocument>(channel.Id).Increment(doc => doc.TotalUnreadMessages);
            await session.SaveChangesAsync();
        }

        (await LoadAsync(channel.Id))!.Version.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateRevision_FromAStaleRead_IsRejected()
    {
        // Two writers read the channel at Version 1. The first write wins; the second is a
        // lost update and must fail instead of silently overwriting the first.
        var channel = NewChannel();
        await InsertAsync(channel);

        await using (var first = fixture.Store.LightweightSession())
        {
            first.UpdateRevision(channel with { IsPinned = true }, 2);
            await first.SaveChangesAsync();
        }

        await using var second = fixture.Store.LightweightSession();
        second.UpdateRevision(channel with { State = ChannelState.Muted }, 2);
        await Should.ThrowAsync<ConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task ListByUserNewestFirst_UsesTheUserIdLastMessageAtIndex()
    {
        await using var query = fixture.Store.QuerySession();
        var command = query.Query<UserChannelDocument>()
            .Where(channel => channel.UserId == "d9360022-d706-4670-bf05-6c7e0a043732")
            .OrderByDescending(channel => channel.LastMessageAt)
            .Take(20)
            .ToCommand();

        var plan = await fixture.ExplainWithoutSeqScanAsync(command);

        plan.ShouldContain("mt_doc_userchanneldocument_idx_user_id_last_message_at");
    }

    private static UserChannelDocument NewChannel()
    {
        var userId = Guid.NewGuid().ToString();
        return new UserChannelDocument
        {
            Id = UserChannelDocument.CreateId(userId, Guid.NewGuid().ToString()),
            ConversationId = "01KX6WMD905AN68KKFWQVDNCHZ",
            UserId = userId
        };
    }

    private async Task InsertAsync(UserChannelDocument channel)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Insert(channel);
        await session.SaveChangesAsync();
    }

    private async Task<UserChannelDocument?> LoadAsync(string id)
    {
        await using var query = fixture.Store.QuerySession();
        return await query.LoadAsync<UserChannelDocument>(id);
    }
}
