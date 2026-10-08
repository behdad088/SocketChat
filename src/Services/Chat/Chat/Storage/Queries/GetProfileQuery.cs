using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Queries;

public record GetProfileParameters(string UserId);

public abstract record GetProfileResult
{
    public record Success(ProfileDocument Profile) : GetProfileResult;

    public record NotFound : GetProfileResult;
}

public delegate Task<GetProfileResult> GetProfile(
    IQuerySession session,
    GetProfileParameters parameters,
    CancellationToken ct);

public class GetProfileQuery
{
    public async Task<GetProfileResult> Execute(
        IQuerySession session,
        GetProfileParameters parameters,
        CancellationToken ct)
    {
        var profile = await session.LoadAsync<ProfileDocument>(parameters.UserId, ct);

        return profile is null or { IsDeleted: true }
            ? new GetProfileResult.NotFound()
            : new GetProfileResult.Success(profile);
    }
}
