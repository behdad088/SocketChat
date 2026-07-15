using Identity.API.Messaging.Events;

namespace Identity.API.Messaging.Outbox;

public interface IOutboxWriter
{
    /// <summary>
    /// Adds the event to the outbox and returns the created OutboxMessage.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="occurredAt">Datetime of the event</param>
    /// <typeparam name="TEvent"></typeparam>
    /// <returns></returns>
    OutboxMessage Enqueue<TEvent>(TEvent @event, DateTimeOffset occurredAt)
        where TEvent : class, IIntegrationEvent;
}
