using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Commands;

public record MarkChannelReadParameters(string UserId, string PeerUserId, string MessageId);

public abstract record MarkChannelReadResult
{
    public record Applied : MarkChannelReadResult;

    public record Ignored : MarkChannelReadResult;

    public record NotFound : MarkChannelReadResult;
}

public delegate Task<MarkChannelReadResult> MarkChannelRead(
    IDocumentSession session,
    MarkChannelReadParameters parameters,
    CancellationToken ct);

public class MarkChannelReadCommand
{
    public async Task<MarkChannelReadResult> Execute(
        IDocumentSession session,
        MarkChannelReadParameters parameters,
        CancellationToken ct)
    {
        if (!Ulid.TryParse(parameters.MessageId, out var messageId) || messageId.ToString() != parameters.MessageId)
            throw new ArgumentException("MessageId must be an uppercase ULID.", nameof(parameters));

        var channel = await session.LoadAsync<UserChannelDocument>(
            UserChannelDocument.CreateId(parameters.UserId, parameters.PeerUserId), ct);
        if (channel is null)
            return new MarkChannelReadResult.NotFound();
        if (string.CompareOrdinal(parameters.MessageId, channel.LastReadMessageId) <= 0)
            return new MarkChannelReadResult.Ignored();

        var unread = await session.Query<MessageDocument>().CountAsync(message =>
            message.ConversationId == channel.ConversationId &&
            message.SenderId == parameters.PeerUserId &&
            !message.IsDeleted &&
            message.Id.CompareTo(parameters.MessageId) > 0, ct);

        // Every write to the channel bumps Version, so if one lands after the load above (a persisted
        // message, a preference change), SaveChangesAsync throws ConcurrencyException and the caller retries.
        session.UpdateRevision(
            channel with { LastReadMessageId = parameters.MessageId, TotalUnreadMessages = unread },
            channel.Version + 1);

        return new MarkChannelReadResult.Applied();
    }
}
