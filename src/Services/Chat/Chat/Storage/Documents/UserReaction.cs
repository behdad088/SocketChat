namespace Chat.Storage.Documents;

public sealed record UserReaction
{
    public required string UserId { get; init; }
    public required string Emoji { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
