namespace Chat.Storage.Documents;

/// <summary>
/// Table: mt_doc_messageversiondocument.
/// </summary>
public sealed record MessageVersionDocument
{
    public required string Id { get; init; }
    public required string ConversationId { get; init; }
    public required string SenderId { get; init; }
    public required string Content { get; init; }
    public string? RepliedTo { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool IsEdited { get; init; }
    public required int Version { get; init; }

    public static string CreateId(string messageId, int version) => $"{messageId}:{version}";
}
