using System.Text.Json.Serialization;
using Identity.API.Models;

namespace Identity.API.Messaging.Events;

public sealed record UserDeletedEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string CloudEventType => EventConstants.UserDeletedCloudEventType;
    public static string ExchangeName => EventConstants.UserDeletedExchangeName;

    // The deletion is the user's last change, so it carries the next version. Consumers then
    // discard any UserUpdated that is delivered after it.
    public static UserDeletedEvent FromUser(ApplicationUser user, DateTimeOffset occurredAt) =>
        new(user.Id, user.Version + 1, occurredAt);
}
