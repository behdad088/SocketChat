namespace Chat.Storage.Documents;

/// <summary>
/// Table: mt_doc_messagedocument. See
/// </summary>
public sealed record MessageDocument
{
    public required string Id { get; init; }
    
    public string? RevisionId { get; init; }
    public required string ConversationId { get; init; }
    public required string SenderId { get; init; }

    public required string Content { get; init; }
    public string? RepliedTo { get; init; }
    public UserReaction[]? Reactions { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool IsEdited { get; init; }
    public bool IsDeleted { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public int Version { get; init; }
}
