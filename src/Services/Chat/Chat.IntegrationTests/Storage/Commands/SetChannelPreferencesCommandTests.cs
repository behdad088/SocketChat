using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using Marten;
using Marten.Patching;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class SetChannelPreferencesCommandTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Set_State_ChangesOnlyTheUsersOwnChannel()
    {
        var (userId, peerUserId) = await StartConversationAsync();

        var result = await SetAsync(userId, peerUserId, ChannelState.Archived, isPinned: null);

        result.ShouldBeOfType<SetChannelPreferencesResult.Applied>();
        var channel = await LoadAsync(userId, peerUserId);
        channel.State.ShouldBe(ChannelState.Archived);
        channel.IsPinned.ShouldBeFalse();
        (await LoadAsync(peerUserId, userId)).State.ShouldBeNull();
    }

    [Fact]
    public async Task Set_IsPinned_KeepsTheStoredState()
    {
        var (userId, peerUserId) = await StartConversationAsync();
        await SetAsync(userId, peerUserId, ChannelState.Muted, isPinned: null);

        await SetAsync(userId, peerUserId, state: null, isPinned: true);

        var channel = await LoadAsync(userId, peerUserId);
        channel.IsPinned.ShouldBeTrue();
        channel.State.ShouldBe(ChannelState.Muted);
    }

    [Fact]
    public async Task Set_StateAndIsPinnedTogether_AppliesBoth()
    {
        var (userId, peerUserId) = await StartConversationAsync();

        await SetAsync(userId, peerUserId, ChannelState.Muted, isPinned: true);

        var channel = await LoadAsync(userId, peerUserId);
        channel.State.ShouldBe(ChannelState.Muted);
        channel.IsPinned.ShouldBeTrue();
    }

    [Fact]
    public async Task Set_NothingToChange_LeavesTheChannelAsItIs()
    {
        var (userId, peerUserId) = await StartConversationAsync();

        var result = await SetAsync(userId, peerUserId, state: null, isPinned: null);

        result.ShouldBeOfType<SetChannelPreferencesResult.Applied>();
        (await LoadAsync(userId, peerUserId)).Version.ShouldBe(1);
    }

    [Fact]
    public async Task Set_StoresStateAsItsName()
    {
        var (userId, peerUserId) = await StartConversationAsync();

        await SetAsync(userId, peerUserId, ChannelState.Archived, isPinned: null);

        (await fixture.GetJsonFieldAsync(
            "mt_doc_userchanneldocument", UserChannelDocument.CreateId(userId, peerUserId), "State")).ShouldBe("Archived");
    }

    [Fact]
    public async Task Set_AfterAnotherWriteToTheChannel_StillApplies()
    {
        var (userId, peerUserId) = await StartConversationAsync();
        await using var slow = fixture.Store.LightweightSession();
        await new SetChannelPreferencesCommand().Execute(
            slow, new SetChannelPreferencesParameters(userId, peerUserId, null, true), CancellationToken.None);

        await using (var other = fixture.Store.LightweightSession())
        {
            other.Patch<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId))
                .Increment(channel => channel.TotalUnreadMessages);
            await other.SaveChangesAsync();
        }

        await slow.SaveChangesAsync();
        var channel = await LoadAsync(userId, peerUserId);
        channel.IsPinned.ShouldBeTrue();
        channel.TotalUnreadMessages.ShouldBe(1);
    }

    [Fact]
    public async Task Set_UnknownChannel_ReturnsNotFoundAndCreatesNothing()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();

        var result = await SetAsync(userId, peerUserId, ChannelState.Muted, isPinned: true);

        result.ShouldBeOfType<SetChannelPreferencesResult.NotFound>();
        await using var query = fixture.Store.QuerySession();
        (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId))).ShouldBeNull();
    }

    private async Task<(string UserId, string PeerUserId)> StartConversationAsync()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();
        await using var session = fixture.Store.LightweightSession();
        await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, DateTimeOffset.UtcNow), CancellationToken.None);
        await session.SaveChangesAsync();
        return (userId, peerUserId);
    }

    private async Task<SetChannelPreferencesResult> SetAsync(
        string userId, string peerUserId, ChannelState? state, bool? isPinned)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new SetChannelPreferencesCommand().Execute(
            session, new SetChannelPreferencesParameters(userId, peerUserId, state, isPinned), CancellationToken.None);
        await session.SaveChangesAsync();
        return result;
    }

    private async Task<UserChannelDocument> LoadAsync(string userId, string peerUserId)
    {
        await using var query = fixture.Store.QuerySession();
        return (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId)))!;
    }
}
