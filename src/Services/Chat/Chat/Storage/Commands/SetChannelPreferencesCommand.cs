using Chat.Storage.Documents;
using Marten;
using Marten.Patching;

namespace Chat.Storage.Commands;

public record SetChannelPreferencesParameters(string UserId, string PeerUserId, ChannelState? State, bool? IsPinned);

public abstract record SetChannelPreferencesResult
{
    public record Applied : SetChannelPreferencesResult;

    public record NotFound : SetChannelPreferencesResult;
}

public delegate Task<SetChannelPreferencesResult> SetChannelPreferences(
    IDocumentSession session,
    SetChannelPreferencesParameters parameters,
    CancellationToken ct);

public class SetChannelPreferencesCommand
{
    public async Task<SetChannelPreferencesResult> Execute(
        IDocumentSession session,
        SetChannelPreferencesParameters parameters,
        CancellationToken ct)
    {
        var channelId = UserChannelDocument.CreateId(parameters.UserId, parameters.PeerUserId);
        if (await session.LoadAsync<UserChannelDocument>(channelId, ct) is null)
            return new SetChannelPreferencesResult.NotFound();

        // Marten fails SaveChangesAsync on a patch with no operations.
        if (parameters is { State: null, IsPinned: null })
            return new SetChannelPreferencesResult.Applied();

        var patch = session.Patch<UserChannelDocument>(channelId);
        if (parameters.State is { } state)
            patch.Set(channel => channel.State, state);
        if (parameters.IsPinned is { } isPinned)
            patch.Set(channel => channel.IsPinned, isPinned);

        return new SetChannelPreferencesResult.Applied();
    }
}
