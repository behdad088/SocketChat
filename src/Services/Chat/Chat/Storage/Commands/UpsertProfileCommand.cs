using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Commands;

public record UpsertProfileParameters(
    string UserId,
    string Username,
    string Email,
    string? Firstname,
    string? Lastname,
    string? ProfilePicture,
    int Version);

public abstract record UpsertProfileResult
{
    public record Applied : UpsertProfileResult;

    public record Ignored : UpsertProfileResult;
}

public delegate Task<UpsertProfileResult> UpsertProfile(
    IDocumentSession session,
    UpsertProfileParameters parameters,
    CancellationToken ct);

public class UpsertProfileCommand
{
    public async Task<UpsertProfileResult> Execute(
        IDocumentSession session,
        UpsertProfileParameters parameters,
        CancellationToken ct)
    {
        var stored = await session.LoadAsync<ProfileDocument>(parameters.UserId, ct);
        if (stored is not null && stored.Version >= parameters.Version)
            return new UpsertProfileResult.Ignored();

        var profile = new ProfileDocument
        {
            Id = parameters.UserId,
            Username = parameters.Username,
            Email = parameters.Email,
            Firstname = NullIfBlank(parameters.Firstname),
            Lastname = NullIfBlank(parameters.Lastname),
            ProfilePicture = NullIfBlank(parameters.ProfilePicture),
            LastOnline = stored?.LastOnline,
            Version = parameters.Version
        };

        // Marten checks the version loaded above when the caller saves: if another write landed in
        // between, SaveChangesAsync throws ConcurrencyException (DocumentAlreadyExistsException for a
        // new user) and the caller retries.
        if (stored is null)
            session.Insert(profile);
        else
            session.Store(profile);

        return new UpsertProfileResult.Applied();
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
