using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using Chat.Storage.Queries;
using Marten;
using Marten.Patching;

namespace Chat.IntegrationTests.Storage.Queries;

[Collection(StorageCollection.Name)]
public class ListUserChannelsQueryTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_OrdersChatsByNewestMessageFirst()
    {
        var userId = Guid.NewGuid().ToString();
        var first = await ChatAsync(userId, minute: 1);
        var third = await ChatAsync(userId, minute: 3);
        var second = await ChatAsync(userId, minute: 2);

        var result = await ListAsync(userId);

        PeersOf(result).ShouldBe([third, second, first]);
        result.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_PutsPinnedChatsFirst()
    {
        var userId = Guid.NewGuid().ToString();
        var oldPinned = await ChatAsync(userId, minute: 1, isPinned: true);
        var newest = await ChatAsync(userId, minute: 5);
        var newerPinned = await ChatAsync(userId, minute: 3, isPinned: true);

        var result = await ListAsync(userId);

        PeersOf(result).ShouldBe([newerPinned, oldPinned, newest]);
    }

    [Fact]
    public async Task List_LeavesOutChatsWithoutAMessage()
    {
        var userId = Guid.NewGuid().ToString();
        await ChatAsync(userId, minute: null);
        var withMessage = await ChatAsync(userId, minute: 1);

        var result = await ListAsync(userId);

        PeersOf(result).ShouldBe([withMessage]);
    }

    [Fact]
    public async Task List_LeavesOutArchivedChatsButKeepsMutedOnes()
    {
        var userId = Guid.NewGuid().ToString();
        var plain = await ChatAsync(userId, minute: 1);
        await ChatAsync(userId, minute: 2, state: ChannelState.Archived);
        var muted = await ChatAsync(userId, minute: 3, state: ChannelState.Muted);

        var result = await ListAsync(userId);

        PeersOf(result).ShouldBe([muted, plain]);
    }

    [Fact]
    public async Task ListArchived_ReturnsOnlyArchivedChats()
    {
        var userId = Guid.NewGuid().ToString();
        await ChatAsync(userId, minute: 1);
        var archived = await ChatAsync(userId, minute: 2, state: ChannelState.Archived);
        var archivedPinned = await ChatAsync(userId, minute: 1, state: ChannelState.Archived, isPinned: true);

        var result = await ListAsync(userId, archived: true);

        PeersOf(result).ShouldBe([archivedPinned, archived]);
    }

    [Fact]
    public async Task List_PagesAcrossThePinnedChatsWithoutGapsOrRepeats()
    {
        var userId = Guid.NewGuid().ToString();
        var unpinned1 = await ChatAsync(userId, minute: 1);
        var pinned1 = await ChatAsync(userId, minute: 2, isPinned: true);
        var unpinned2 = await ChatAsync(userId, minute: 3);
        var pinned2 = await ChatAsync(userId, minute: 4, isPinned: true);
        var unpinned3 = await ChatAsync(userId, minute: 5);

        var page1 = await ListAsync(userId, limit: 2);
        var page2 = await ListAsync(userId, limit: 2, after: page1.Next);
        var page3 = await ListAsync(userId, limit: 2, after: page2.Next);

        PeersOf(page1).ShouldBe([pinned2, pinned1]);
        page1.Next.ShouldNotBeNull().IsPinned.ShouldBeTrue();
        PeersOf(page2).ShouldBe([unpinned3, unpinned2]);
        page2.Next.ShouldNotBeNull().IsPinned.ShouldBeFalse();
        PeersOf(page3).ShouldBe([unpinned1]);
        page3.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_PagesThroughMorePinnedChatsThanFitOnAPage()
    {
        var userId = Guid.NewGuid().ToString();
        var unpinned1 = await ChatAsync(userId, minute: 1);
        var pinned1 = await ChatAsync(userId, minute: 2, isPinned: true);
        var unpinned2 = await ChatAsync(userId, minute: 3);
        var pinned2 = await ChatAsync(userId, minute: 4, isPinned: true);
        var pinned3 = await ChatAsync(userId, minute: 5, isPinned: true);

        var page1 = await ListAsync(userId, limit: 2);
        var page2 = await ListAsync(userId, limit: 2, after: page1.Next);
        var page3 = await ListAsync(userId, limit: 2, after: page2.Next);

        PeersOf(page1).ShouldBe([pinned3, pinned2]);
        PeersOf(page2).ShouldBe([pinned1, unpinned2]);
        PeersOf(page3).ShouldBe([unpinned1]);
        page3.Next.ShouldBeNull();
    }

    [Fact]
    public async Task List_WithAZeroLimit_Throws()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => ListAsync(Guid.NewGuid().ToString(), limit: 0));
    }

    [Fact]
    public async Task List_UsesTheChatListIndex()
    {
        var logger = new RecordingSessionLogger();
        await using var query = fixture.Store.QuerySession();
        query.Logger = logger;

        await new ListUserChannelsQuery().Execute(
            query,
            new ListUserChannelsParameters(
                Guid.NewGuid().ToString(), Archived: false, Limit: 20,
                After: new ChatListCursor(IsPinned: true, Ulid.NewUlid().ToString())),
            CancellationToken.None);

        var plan = await fixture.ExplainWithoutSeqScanAsync(logger.Commands.Single());
        plan.ShouldContain("mt_doc_userchanneldocument_idx_chat_list");
    }

    private static IEnumerable<string> PeersOf(ListUserChannelsResult result) =>
        result.Channels.Select(channel => channel.PeerUserId);

    // Starts a conversation and gives the user's channel a last message at Start + minute, the way
    // persisting a message will.
    private async Task<string> ChatAsync(
        string userId, int? minute, ChannelState? state = null, bool isPinned = false)
    {
        var peerUserId = Guid.NewGuid().ToString();
        await using var session = fixture.Store.LightweightSession();
        await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, Start), CancellationToken.None);
        await session.SaveChangesAsync();

        if (minute is { } at)
        {
            var lastMessageAt = Start.AddMinutes(at);
            session.Patch<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId))
                .Set(channel => channel.LastMessageId, Ulid.NewUlid(lastMessageAt).ToString())
                .Set(channel => channel.LastMessageAt, lastMessageAt);
        }

        if (state is not null || isPinned)
            await new SetChannelPreferencesCommand().Execute(
                session, new SetChannelPreferencesParameters(userId, peerUserId, state, isPinned), CancellationToken.None);

        await session.SaveChangesAsync();
        return peerUserId;
    }

    private async Task<ListUserChannelsResult> ListAsync(
        string userId, bool archived = false, int limit = 20, ChatListCursor? after = null)
    {
        await using var query = fixture.Store.QuerySession();
        return await new ListUserChannelsQuery().Execute(
            query, new ListUserChannelsParameters(userId, archived, limit, after), CancellationToken.None);
    }
}
