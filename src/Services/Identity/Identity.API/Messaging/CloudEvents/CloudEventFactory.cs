using System.Net.Mime;
using Identity.API.Messaging.Events;

namespace Identity.API.Messaging.CloudEvents;

public static class CloudEventFactory
{
    private const string Source = "urn:socketchat:identity-api";

    /// <summary>
    /// 
    /// </summary>
    /// <param name="event"></param>
    /// <param name="id"></param>
    /// <param name="time"></param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns>an instance of a cloud event</returns>
    public static CloudEvent<TEvent> Create<TEvent>(TEvent @event, Guid id, DateTimeOffset time)
        where TEvent : IIntegrationEvent => new()
    {
        Id = id.ToString(),
        Type = TEvent.CloudEventType,
        Source = Source,
        SpecVersion = "1.0",
        DataContentType = MediaTypeNames.Application.Json,
        Time = time,
        Data = @event
    };
}
