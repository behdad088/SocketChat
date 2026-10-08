namespace Chat.Storage.Documents;

/// <summary>
/// Table: mt_doc_profiledocument.
/// </summary>
public sealed record ProfileDocument
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public string? Firstname { get; init; }
    public string? Lastname { get; init; }
    public string? DisplayName { get; init; }
    public required string Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? ProfilePicture { get; init; }
    public bool? IsActive { get; init; }
    public DateTimeOffset? LastOnline { get; init; }
    public string? Quote { get; init; }
    public bool IsDeleted { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }

    public required int Version { get; init; }
}
