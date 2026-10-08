using System.Text.Json.Serialization;
using Chat.Storage.Commands;
using FluentValidation;
using Marten;
using Shared;

namespace Chat.EventProcessor.Messaging.Identity;

public sealed record UserDeletedEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt) : IIdentityEvent
{
    public static string CloudEventType => "com.socketchat.identity.user.deleted";
    public static string ExchangeName => "identity.user.deleted";
}

public sealed class UserDeletedEventValidator : AbstractValidator<UserDeletedEvent>
{
    public UserDeletedEventValidator()
    {
        RuleFor(x => x.Id).MustBeValidGuid();
        RuleFor(x => x.Version).GreaterThan(0);
        RuleFor(x => x.OccurredAt).NotEmpty();
    }
}

public sealed class UserDeletedHandler(DeleteUserData deleteUserData) : IIdentityEventHandler<UserDeletedEvent>
{
    public async Task<bool> Apply(IDocumentSession session, UserDeletedEvent @event, CancellationToken ct)
    {
        var result = await deleteUserData(
            session,
            new DeleteUserDataParameters(@event.Id, @event.Version, @event.OccurredAt),
            ct);

        return result is DeleteUserDataResult.Applied;
    }
}
