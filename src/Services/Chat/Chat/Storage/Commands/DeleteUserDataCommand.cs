using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Commands;

public record DeleteUserDataParameters(string UserId, int Version, DateTimeOffset DeletedAt);

public abstract record DeleteUserDataResult
{
    public record Applied : DeleteUserDataResult;

    public record Ignored : DeleteUserDataResult;
}

public delegate Task<DeleteUserDataResult> DeleteUserData(
    IDocumentSession session,
    DeleteUserDataParameters parameters,
    CancellationToken ct);

public class DeleteUserDataCommand
{
    public async Task<DeleteUserDataResult> Execute(
        IDocumentSession session,
        DeleteUserDataParameters parameters,
        CancellationToken ct)
    {
        var stored = await session.LoadAsync<ProfileDocument>(parameters.UserId, ct);
        if (stored is not null && stored.Version >= parameters.Version)
            return new DeleteUserDataResult.Ignored();

        var tombstone = new ProfileDocument
        {
            Id = parameters.UserId,
            Username = string.Empty,
            Email = string.Empty,
            IsDeleted = true,
            DeletedAt = parameters.DeletedAt,
            Version = parameters.Version
        };

        if (stored is null)
            session.Insert(tombstone);
        else
            session.Store(tombstone);

        return new DeleteUserDataResult.Applied();
    }
}
