namespace Chat.Storage.Documents;

/// <summary>
/// Table: mt_doc_conversationdocument.
/// </summary>
public sealed record ConversationDocument
{
    public required string Id { get; init; }
    public required string[] Participants { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    public string? LastMessageId { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
}
