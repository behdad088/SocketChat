using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Queries;

namespace Chat.IntegrationTests.Storage.Queries;

[Collection(StorageCollection.Name)]
public class GetConversationQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetConversation_StartedConversation_ReturnsItWithBothParticipants()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();
        string conversationId;
        await using (var session = fixture.Store.LightweightSession())
        {
            conversationId = (await new StartConversationCommand().Execute(
                session, new StartConversationParameters(userId, peerUserId, DateTimeOffset.UtcNow), CancellationToken.None))
                .ConversationId;
            await session.SaveChangesAsync();
        }

        var result = await ExecuteAsync(conversationId);

        var conversation = result.ShouldBeOfType<GetConversationResult.Success>().Conversation;
        conversation.Id.ShouldBe(conversationId);
        conversation.Participants.ShouldBe([userId, peerUserId]);
    }

    [Fact]
    public async Task GetConversation_UnknownId_ReturnsNotFound()
    {
        var result = await ExecuteAsync(Ulid.NewUlid().ToString());

        result.ShouldBeOfType<GetConversationResult.NotFound>();
    }

    private async Task<GetConversationResult> ExecuteAsync(string conversationId)
    {
        await using var query = fixture.Store.QuerySession();
        return await new GetConversationQuery().Execute(
            query, new GetConversationParameters(conversationId), CancellationToken.None);
    }
}
