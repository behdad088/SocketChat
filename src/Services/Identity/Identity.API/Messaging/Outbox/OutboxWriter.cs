using System.Diagnostics;
using System.Text.Json;
using Identity.API.Data;
using Identity.API.Messaging.Events;

namespace Identity.API.Messaging.Outbox;

public sealed class OutboxWriter(ApplicationDbContext dbContext) : IOutboxWriter
{
    public OutboxMessage Enqueue<TEvent>(TEvent @event, DateTimeOffset occurredAt)
        where TEvent : class, IIntegrationEvent
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = TEvent.CloudEventType,
            Payload = JsonSerializer.Serialize(@event),
            TraceParent = Activity.Current?.Id,
            OccurredAt = occurredAt,
            NextAttemptAt = occurredAt
        };

        dbContext.OutboxMessages.Add(message);
        return message;
    }
}
