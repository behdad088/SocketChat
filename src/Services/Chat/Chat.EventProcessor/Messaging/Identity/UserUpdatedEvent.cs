using System.Text.Json.Serialization;
using Chat.Storage.Commands;
using FluentValidation;
using Marten;
using Shared;

namespace Chat.EventProcessor.Messaging.Identity;

public sealed record UserUpdatedEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("profile_picture")] string? ProfilePicture,
    [property: JsonPropertyName("version")] int Version) : IIdentityEvent
{
    public static string CloudEventType => "com.socketchat.identity.user.updated";
    public static string ExchangeName => "identity.user.updated";
}

public sealed class UserUpdatedEventValidator : AbstractValidator<UserUpdatedEvent>
{
    public UserUpdatedEventValidator()
    {
        RuleFor(x => x.Id).MustBeValidGuid();
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Username).NotEmpty();
        RuleFor(x => x.Version).GreaterThanOrEqualTo(0);
    }
}

public sealed class UserUpdatedHandler(UpsertProfile upsertProfile) : IIdentityEventHandler<UserUpdatedEvent>
{
    public async Task<bool> Apply(IDocumentSession session, UserUpdatedEvent @event, CancellationToken ct)
    {
        var result = await upsertProfile(
            session,
            new UpsertProfileParameters(
                @event.Id,
                @event.Username,
                @event.Email,
                @event.Name,
                @event.LastName,
                @event.ProfilePicture,
                @event.Version),
            ct);

        return result is UpsertProfileResult.Applied;
    }
}
