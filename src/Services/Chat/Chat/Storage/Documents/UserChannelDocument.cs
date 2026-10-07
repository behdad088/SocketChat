namespace Chat.Storage.Documents;

/// <summary>
/// per participant. Table: mt_doc_userchanneldocument.,
/// "User Channel Document".
/// </summary>
public sealed record UserChannelDocument
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public required string UserId { get; init; }
    public ChannelState? State { get; init; }
    public bool IsPinned { get; init; }
    public bool IsPrivate { get; init; }

    public string? LastReadMessageId { get; init; }

    public string? LastMessageId { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public string? LatestMessageBody { get; init; }
    public int TotalUnreadMessages { get; init; }

    public int Version { get; init; }

    public static string CreateId(string userId, string peerUserId) => $"{userId}:{peerUserId}";
}
