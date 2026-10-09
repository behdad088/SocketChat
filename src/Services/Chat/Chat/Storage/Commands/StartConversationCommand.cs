using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Commands;

public record StartConversationParameters(string UserId, string PeerUserId, DateTimeOffset CreatedAt);

public abstract record StartConversationResult(string ConversationId)
{
    public record Started(string ConversationId) : StartConversationResult(ConversationId);

    public record Existing(string ConversationId) : StartConversationResult(ConversationId);
}

public delegate Task<StartConversationResult> StartConversation(
    IDocumentSession session,
    StartConversationParameters parameters,
    CancellationToken ct);

public class StartConversationCommand
{
    public async Task<StartConversationResult> Execute(
        IDocumentSession session,
        StartConversationParameters parameters,
        CancellationToken ct)
    {
        if (parameters.UserId == parameters.PeerUserId)
            throw new ArgumentException("A conversation needs two different users.", nameof(parameters));

        var channel = await session.LoadAsync<UserChannelDocument>(
            UserChannelDocument.CreateId(parameters.UserId, parameters.PeerUserId), ct);
        if (channel is not null)
            return new StartConversationResult.Existing(channel.ConversationId);

        var conversationId = Ulid.NewUlid(parameters.CreatedAt).ToString();
        session.Insert(new ConversationDocument
        {
            Id = conversationId,
            Participants = [parameters.UserId, parameters.PeerUserId],
            CreatedAt = parameters.CreatedAt
        });

        // When both users start the conversation at once, the second SaveChangesAsync throws
        // DocumentAlreadyExistsException on these ids and rolls back; the caller retries and gets Existing.
        session.Insert(NewChannel(conversationId, parameters.UserId, parameters.PeerUserId));
        session.Insert(NewChannel(conversationId, parameters.PeerUserId, parameters.UserId));

        return new StartConversationResult.Started(conversationId);
    }

    private static UserChannelDocument NewChannel(string conversationId, string userId, string peerUserId) => new()
    {
        Id = UserChannelDocument.CreateId(userId, peerUserId),
        ConversationId = conversationId,
        UserId = userId,
        PeerUserId = peerUserId
    };
}
