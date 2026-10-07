namespace Chat.Storage.Documents;

/// <summary>
/// Conversation-level data shared by all participants. Table: mt_doc_conversationdocument.
/// See src/docs/database/schema.md, "Conversation Document".
/// </summary>
public sealed record ConversationDocument
{
    /// <summary>A ULID.</summary>
    public required string Id { get; init; }
    public required string[] Participants { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>ULID of the newest message reflected in LastMessageAt.</summary>
    public string? LastMessageId { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
}
