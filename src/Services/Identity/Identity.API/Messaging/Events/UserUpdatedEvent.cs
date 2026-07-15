using System.Text.Json.Serialization;
using Identity.API.Models;

namespace Identity.API.Messaging.Events;

public sealed record UserUpdatedEvent(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("profile_picture")] string ProfilePicture,
    [property: JsonPropertyName("email_confirmed")] bool EmailConfirmed,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string CloudEventType => EventConstants.UserUpdatedCloudEventType;
    public static string ExchangeName => EventConstants.UserUpdatedExchangeName;

    public static UserUpdatedEvent FromUser(ApplicationUser user, DateTimeOffset occurredAt) => new(
        user.Id,
        user.Email!,
        user.UserName!,
        user.Name, 
        user.LastName,
        user.ProfilePicture,
        user.EmailConfirmed,
        user.Version,
        occurredAt);
}
