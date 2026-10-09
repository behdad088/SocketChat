using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Queries;

public record ChatListCursor(bool IsPinned, string LastMessageId);

public record ListUserChannelsParameters(string UserId, bool Archived, int Limit, ChatListCursor? After);

public record ListUserChannelsResult(IReadOnlyList<UserChannelDocument> Channels, ChatListCursor? Next);

public delegate Task<ListUserChannelsResult> ListUserChannels(
    IQuerySession session,
    ListUserChannelsParameters parameters,
    CancellationToken ct);

public class ListUserChannelsQuery
{
    public async Task<ListUserChannelsResult> Execute(
        IQuerySession session,
        ListUserChannelsParameters parameters,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parameters.Limit);

        IQueryable<UserChannelDocument> channels = session.Query<UserChannelDocument>()
            .Where(channel => channel.UserId == parameters.UserId && channel.LastMessageId != null);

        channels = parameters.Archived
            ? channels.Where(channel => channel.State == ChannelState.Archived)
            : channels.Where(channel => channel.State == null || channel.State != ChannelState.Archived);

        // Pinned chats come first, so after a pinned cursor every unpinned chat is still ahead.
        if (parameters.After is { IsPinned: true } pinnedCursor)
            channels = channels.Where(channel =>
                !channel.IsPinned || channel.LastMessageId!.CompareTo(pinnedCursor.LastMessageId) < 0);
        else if (parameters.After is { } cursor)
            channels = channels.Where(channel =>
                !channel.IsPinned && channel.LastMessageId!.CompareTo(cursor.LastMessageId) < 0);

        var page = await channels
            .OrderByDescending(channel => channel.IsPinned)
            .ThenByDescending(channel => channel.LastMessageId)
            .Take(parameters.Limit + 1)
            .ToListAsync(ct);

        if (page.Count <= parameters.Limit)
            return new ListUserChannelsResult(page, null);

        var result = page.Take(parameters.Limit).ToList();
        var last = result[^1];
        return new ListUserChannelsResult(result, new ChatListCursor(last.IsPinned, last.LastMessageId!));
    }
}
