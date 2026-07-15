using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;

namespace Identity.API.Messaging.Publishing;

public interface IEventPublisher<TEvent> where TEvent : class, IIntegrationEvent
{
    /// <summary>Wraps the event in a CloudEvent and publishes it. Throws on failure.</summary>
    Task PublishAsync(OutboxMessage message, TEvent @event, CancellationToken cancellationToken);
}
