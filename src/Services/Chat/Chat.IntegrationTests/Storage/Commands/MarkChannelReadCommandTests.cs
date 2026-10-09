using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using JasperFx;
using Marten.Patching;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class MarkChannelReadCommandTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MarkRead_CountsOnlyThePeersMessagesAfterTheReadPosition()
    {
        var chat = await StartAsync();
        await SendAsync(chat, chat.PeerUserId, minute: 1);
        var read = await SendAsync(chat, chat.PeerUserId, minute: 2);
        await SendAsync(chat, chat.UserId, minute: 3);
        await SendAsync(chat, chat.PeerUserId, minute: 4);
        await SendAsync(chat, chat.PeerUserId, minute: 5);

        var result = await MarkReadAsync(chat, read);

        result.ShouldBeOfType<MarkChannelReadResult.Applied>();
        var channel = await LoadAsync(chat.UserId, chat.PeerUserId);
        channel.LastReadMessageId.ShouldBe(read);
        channel.TotalUnreadMessages.ShouldBe(2);
    }

    [Fact]
    public async Task MarkRead_DoesNotCountDeletedMessages()
    {
        var chat = await StartAsync();
        var read = await SendAsync(chat, chat.PeerUserId, minute: 1);
        await SendAsync(chat, chat.PeerUserId, minute: 2, isDeleted: true);
        await SendAsync(chat, chat.PeerUserId, minute: 3);

        await MarkReadAsync(chat, read);

        (await LoadAsync(chat.UserId, chat.PeerUserId)).TotalUnreadMessages.ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_SameOrOlderMessage_IsIgnored()
    {
        var chat = await StartAsync();
        var older = await SendAsync(chat, chat.PeerUserId, minute: 1);
        var read = await SendAsync(chat, chat.PeerUserId, minute: 2);
        await MarkReadAsync(chat, read);

        (await MarkReadAsync(chat, read)).ShouldBeOfType<MarkChannelReadResult.Ignored>();
        (await MarkReadAsync(chat, older)).ShouldBeOfType<MarkChannelReadResult.Ignored>();

        (await LoadAsync(chat.UserId, chat.PeerUserId)).LastReadMessageId.ShouldBe(read);
    }

    [Fact]
    public async Task MarkRead_AMessageThatIsNotPersistedYet_LeavesNothingUnread()
    {
        var chat = await StartAsync();
        await SendAsync(chat, chat.PeerUserId, minute: 1);
        await SendAsync(chat, chat.PeerUserId, minute: 2);
        var pending = Ulid.NewUlid(Start.AddMinutes(3)).ToString();

        var result = await MarkReadAsync(chat, pending);

        result.ShouldBeOfType<MarkChannelReadResult.Applied>();
        var channel = await LoadAsync(chat.UserId, chat.PeerUserId);
        channel.LastReadMessageId.ShouldBe(pending);
        channel.TotalUnreadMessages.ShouldBe(0);
    }

    [Fact]
    public async Task MarkRead_ChangesOnlyTheReadersChannel()
    {
        var chat = await StartAsync();
        var read = await SendAsync(chat, chat.PeerUserId, minute: 1);

        await MarkReadAsync(chat, read);

        var peerChannel = await LoadAsync(chat.PeerUserId, chat.UserId);
        peerChannel.LastReadMessageId.ShouldBeNull();
        peerChannel.Version.ShouldBe(1);
    }

    [Theory]
    [InlineData("not-a-ulid")]
    [InlineData("01kx6y4mjq9512txyzb2cthgp5")]
    public async Task MarkRead_WithAnIdThatIsNotAnUppercaseUlid_ThrowsAndKeepsTheReadPosition(string messageId)
    {
        var chat = await StartAsync();

        await Should.ThrowAsync<ArgumentException>(() => MarkReadAsync(chat, messageId));

        (await LoadAsync(chat.UserId, chat.PeerUserId)).LastReadMessageId.ShouldBeNull();
    }

    [Fact]
    public async Task MarkRead_UnknownChannel_ReturnsNotFound()
    {
        var chat = new Chat(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Ulid.NewUlid().ToString());

        var result = await MarkReadAsync(chat, Ulid.NewUlid().ToString());

        result.ShouldBeOfType<MarkChannelReadResult.NotFound>();
    }

    [Fact]
    public async Task MarkRead_RacingAMessageBeingPersisted_IsRejectedAndCountsItOnRetry()
    {
        var chat = await StartAsync();
        var read = await SendAsync(chat, chat.PeerUserId, minute: 1);
        await SendAsync(chat, chat.PeerUserId, minute: 2);
        await using var slow = fixture.Store.LightweightSession();
        await new MarkChannelReadCommand().Execute(
            slow, new MarkChannelReadParameters(chat.UserId, chat.PeerUserId, read), CancellationToken.None);

        await SendAsync(chat, chat.PeerUserId, minute: 3, countAsUnread: true);

        await Should.ThrowAsync<ConcurrencyException>(() => slow.SaveChangesAsync());
        await MarkReadAsync(chat, read);
        (await LoadAsync(chat.UserId, chat.PeerUserId)).TotalUnreadMessages.ShouldBe(2);
    }

    [Fact]
    public async Task MarkRead_UnreadCount_UsesTheConversationIdIdIndex()
    {
        var chat = await StartAsync();
        var logger = new RecordingSessionLogger();
        await using var session = fixture.Store.LightweightSession();
        session.Logger = logger;

        await new MarkChannelReadCommand().Execute(
            session, new MarkChannelReadParameters(chat.UserId, chat.PeerUserId, Ulid.NewUlid().ToString()),
            CancellationToken.None);

        var count = logger.Commands.Single(command => command.CommandText.Contains("count("));
        (await fixture.ExplainWithoutSeqScanAsync(count)).ShouldContain("mt_doc_messagedocument_idx_conversation_id_id");
    }

    private sealed record Chat(string UserId, string PeerUserId, string ConversationId);

    private async Task<Chat> StartAsync()
    {
        var userId = Guid.NewGuid().ToString();
        var peerUserId = Guid.NewGuid().ToString();
        await using var session = fixture.Store.LightweightSession();
        var result = await new StartConversationCommand().Execute(
            session, new StartConversationParameters(userId, peerUserId, Start), CancellationToken.None);
        await session.SaveChangesAsync();
        return new Chat(userId, peerUserId, result.ConversationId);
    }

    // Persists a message at Start + minute. With countAsUnread it also bumps the reader's unread count,
    // the way persisting a message will.
    private async Task<string> SendAsync(
        Chat chat, string senderId, int minute, bool isDeleted = false, bool countAsUnread = false)
    {
        var createdAt = Start.AddMinutes(minute);
        var messageId = Ulid.NewUlid(createdAt).ToString();
        await using var session = fixture.Store.LightweightSession();
        session.Insert(new MessageDocument
        {
            Id = messageId,
            ConversationId = chat.ConversationId,
            SenderId = senderId,
            Content = isDeleted ? string.Empty : "test",
            CreatedAt = createdAt,
            IsDeleted = isDeleted,
            DeletedAt = isDeleted ? createdAt : null
        });
        if (countAsUnread)
            session.Patch<UserChannelDocument>(UserChannelDocument.CreateId(chat.UserId, chat.PeerUserId))
                .Increment(channel => channel.TotalUnreadMessages);
        await session.SaveChangesAsync();
        return messageId;
    }

    private async Task<MarkChannelReadResult> MarkReadAsync(Chat chat, string messageId)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new MarkChannelReadCommand().Execute(
            session, new MarkChannelReadParameters(chat.UserId, chat.PeerUserId, messageId), CancellationToken.None);
        await session.SaveChangesAsync();
        return result;
    }

    private async Task<UserChannelDocument> LoadAsync(string userId, string peerUserId)
    {
        await using var query = fixture.Store.QuerySession();
        return (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerUserId)))!;
    }
}
