namespace Chat.Storage.Documents;

/// <summary>
/// Table: mt_doc_profiledocument.
/// </summary>
public sealed record ProfileDocument
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public required string Firstname { get; init; }
    public required string Lastname { get; init; }
    public string? DisplayName { get; init; }
    public required string Email { get; init; }
    public required string PhoneNumber { get; init; }
    public string? ProfilePicture { get; init; }
    public bool? IsActive { get; init; }
    public DateTimeOffset? LastOnline { get; init; }
    public string? Quote { get; init; }

    /// <summary>
    /// Mirrors the Identity user version. The profile-sync write must skip an event whose
    /// Version is less than or equal to this one, and must keep the Chat-owned LastOnline.
    /// Writing LastOnline doesn't change it.
    /// </summary>
    public required int Version { get; init; }
}
