using Chat.IntegrationTests.Infrastructure;
using Chat.Storage;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using JasperFx;
using Marten;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class StartConversationCommandTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Start_NewPair_CreatesTheConversationAndAChannelForEachUser()
    {
        var (userId, peerUserId) = NewPair();

        var result = await StartAsync(userId, peerUserId);

        var started = result.ShouldBeOfType<StartConversationResult.Started>();
        Ulid.TryParse(started.ConversationId, out _).ShouldBeTrue();
        await using var query = fixture.Store.QuerySession();
        var conversation = await query.LoadAsync<ConversationDocument>(started.ConversationId);
        conversation.ShouldNotBeNull();
        conversation.Participants.ShouldBe([userId, peerUserId]);
        conversation.CreatedAt.ShouldBe(CreatedAt);
        conversation.LastMessageId.ShouldBeNull();
        (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId)))
            .ShouldBe(NewChannel(started.ConversationId, userId, peerUserId));
        (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(peerUserId, userId)))
            .ShouldBe(NewChannel(started.ConversationId, peerUserId, userId));
    }

    [Fact]
    public async Task Start_ExistingPair_ReturnsTheSameConversationForBothUsers()
    {
        var (userId, peerUserId) = NewPair();
        var started = await StartAsync(userId, peerUserId);

        var again = await StartAsync(userId, peerUserId);
        var fromPeer = await StartAsync(peerUserId, userId);

        again.ShouldBe(new StartConversationResult.Existing(started.ConversationId));
        fromPeer.ShouldBe(new StartConversationResult.Existing(started.ConversationId));
    }

    [Fact]
    public async Task Start_FromAStaleRead_IsRejectedAndRolledBack()
    {
        var (userId, peerUserId) = NewPair();
        await using var slow = fixture.Store.LightweightSession();
        var lost = await new StartConversationCommand().Execute(
            slow, new StartConversationParameters(peerUserId, userId, CreatedAt), CancellationToken.None);

        var won = await StartAsync(userId, peerUserId);

        var conflict = await Should.ThrowAsync<AggregateException>(() => slow.SaveChangesAsync());
        conflict.InnerExceptions.ShouldAllBe(exception => exception is DocumentAlreadyExistsException);
        conflict.IsWriteConflict().ShouldBeTrue();
        await using var query = fixture.Store.QuerySession();
        (await query.LoadAsync<ConversationDocument>(lost.ConversationId)).ShouldBeNull();
        (await StartAsync(peerUserId, userId)).ShouldBe(new StartConversationResult.Existing(won.ConversationId));
    }

    [Fact]
    public async Task Start_ConcurrentlyFromBothSides_GivesEveryCallerTheSameConversation()
    {
        var (userId, peerUserId) = NewPair();

        var conversationIds = await Task.WhenAll(Enumerable.Range(0, 10).Select(attempt =>
            attempt % 2 == 0 ? StartWithRetryAsync(userId, peerUserId) : StartWithRetryAsync(peerUserId, userId)));

        conversationIds.Distinct().ShouldHaveSingleItem();
        await using var query = fixture.Store.QuerySession();
        (await query.Query<ConversationDocument>().CountAsync(conversation => conversation.Participants.Contains(userId)))
            .ShouldBe(1);
    }

    [Fact]
    public async Task Start_WithYourself_Throws()
    {
        var userId = Guid.NewGuid().ToString();

        await Should.ThrowAsync<ArgumentException>(() => StartAsync(userId, userId));
    }

    private static (string UserId, string PeerUserId) NewPair() => (Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

    private static UserChannelDocument NewChannel(string conversationId, string userId, string peerUserId) => new()
    {
        Id = UserChannelDocument.CreateId(userId, peerUserId),
        ConversationId = conversationId,
        UserId = userId,
        PeerUserId = peerUserId,
        Version = 1
    };

    private async Task<StartConversationResult> StartAsync(string userId, string peerUserId)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, CreatedAt), CancellationToken.None);
        await session.SaveChangesAsync();
        return result;
    }

    private async Task<string> StartWithRetryAsync(string userId, string peerUserId)
    {
        StartConversationResult? result = null;
        await fixture.SaveWithRetryAsync(async session => result = await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, CreatedAt), CancellationToken.None));
        return result!.ConversationId;
    }
}
