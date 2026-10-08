using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Queries;

public record GetProfilesParameters(IReadOnlyCollection<string> UserIds);

public delegate Task<IReadOnlyList<ProfileDocument>> GetProfiles(
    IQuerySession session,
    GetProfilesParameters parameters,
    CancellationToken ct);

public class GetProfilesQuery
{
    public async Task<IReadOnlyList<ProfileDocument>> Execute(
        IQuerySession session,
        GetProfilesParameters parameters,
        CancellationToken ct)
    {
        if (parameters.UserIds.Count == 0)
            return [];

        return await session.LoadManyAsync<ProfileDocument>(ct, parameters.UserIds);
    }
}
