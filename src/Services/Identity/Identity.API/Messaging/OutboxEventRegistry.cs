using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Identity.API.Messaging.CloudEvents;
using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;
using Identity.API.Messaging.Publishing;
using MassTransit;

namespace Identity.API.Messaging;

/// <summary>
/// Registers an event type with its RabbitMQ exchange and the logic needed
/// to dispatch it from the outbox. The event is looked up using the
/// OutboxMessage.EventType value.
/// </summary>
public sealed class OutboxEventRegistry
{
    private readonly List<Action<IRabbitMqBusFactoryConfigurator>> _topology = [];
    private readonly Dictionary<string, Func<IServiceProvider, OutboxMessage, CancellationToken, Task>> _dispatchers = [];

    public void Add<TEvent>() where TEvent : class, IIntegrationEvent
    {
        _topology.Add(cfg =>
        {
            cfg.Message<CloudEvent<TEvent>>(c => c.SetEntityName(TEvent.ExchangeName));
            cfg.Publish<CloudEvent<TEvent>>(p => p.ExchangeType = RabbitMQ.Client.ExchangeType.Fanout);
        });

        _dispatchers[TEvent.CloudEventType] = async (serviceProvider, message, cancellationToken) =>
        {
            var @event = JsonSerializer.Deserialize<TEvent>(message.Payload)
                ?? throw new InvalidOperationException(
                    $"Outbox message {message.Id} payload deserialized to null for event type '{message.EventType}'.");
            var publisher = serviceProvider.GetRequiredService<IEventPublisher<TEvent>>();
            await publisher.PublishAsync(message, @event, cancellationToken);
        };
    }

    public void ConfigureTopology(IRabbitMqBusFactoryConfigurator cfg)
    {
        foreach (var configure in _topology)
        {
            configure(cfg);
        }
    }

    public bool TryGetDispatcher(
        string eventType,
        [NotNullWhen(true)] out Func<IServiceProvider, OutboxMessage, CancellationToken, Task>? dispatcher) =>
        _dispatchers.TryGetValue(eventType, out dispatcher);
}
