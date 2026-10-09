using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Queries;

namespace Chat.IntegrationTests.Storage.Queries;

[Collection(StorageCollection.Name)]
public class GetChannelQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetChannel_StartedConversation_ReturnsTheUsersOwnChannel()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();
        var conversationId = await StartAsync(userId, peerUserId);

        var result = await ExecuteAsync(userId, peerUserId);

        var channel = result.ShouldBeOfType<GetChannelResult.Success>().Channel;
        channel.ConversationId.ShouldBe(conversationId);
        channel.UserId.ShouldBe(userId);
        channel.PeerUserId.ShouldBe(peerUserId);
    }

    [Fact]
    public async Task GetChannel_UnknownPair_ReturnsNotFound()
    {
        var result = await ExecuteAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        result.ShouldBeOfType<GetChannelResult.NotFound>();
    }

    private async Task<string> StartAsync(string userId, string peerUserId)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, DateTimeOffset.UtcNow), CancellationToken.None);
        await session.SaveChangesAsync();
        return result.ConversationId;
    }

    private async Task<GetChannelResult> ExecuteAsync(string userId, string peerUserId)
    {
        await using var query = fixture.Store.QuerySession();
        return await new GetChannelQuery().Execute(
            query, new GetChannelParameters(userId, peerUserId), CancellationToken.None);
    }
}
