using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Queries;

public record GetChannelParameters(string UserId, string PeerUserId);

public abstract record GetChannelResult
{
    public record Success(UserChannelDocument Channel) : GetChannelResult;

    public record NotFound : GetChannelResult;
}

public delegate Task<GetChannelResult> GetChannel(
    IQuerySession session,
    GetChannelParameters parameters,
    CancellationToken ct);

public class GetChannelQuery
{
    public async Task<GetChannelResult> Execute(
        IQuerySession session,
        GetChannelParameters parameters,
        CancellationToken ct)
    {
        var channel = await session.LoadAsync<UserChannelDocument>(
            UserChannelDocument.CreateId(parameters.UserId, parameters.PeerUserId), ct);

        return channel is null
            ? new GetChannelResult.NotFound()
            : new GetChannelResult.Success(channel);
    }
}
