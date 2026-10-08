using Chat.Storage.Documents;
using Marten;
using Marten.Patching;

namespace Chat.Storage.Commands;

public record SetLastOnlineParameters(string UserId, DateTimeOffset LastOnline);

public delegate void SetLastOnline(IDocumentSession session, SetLastOnlineParameters parameters);

public class SetLastOnlineCommand
{
    public void Execute(IDocumentSession session, SetLastOnlineParameters parameters) =>
        session
            .Patch<ProfileDocument>(profile =>
                profile.Id == parameters.UserId &&
                !profile.IsDeleted &&
                (profile.LastOnline == null || profile.LastOnline < parameters.LastOnline))
            .Set(profile => profile.LastOnline, parameters.LastOnline);
}
