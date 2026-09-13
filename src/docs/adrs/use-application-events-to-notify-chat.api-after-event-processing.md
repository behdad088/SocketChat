Use Application Events to Notify Chat.Api After Event Processing

**Status**: Accepted
**Date**: 2026-09-07

## Context

The chat system consists of two services:

* Chat.Api: exposes the API and SignalR hubs.
* Chat.EventProcessor: consumes RabbitMQ events and performs asynchronous message processing.

The current high-level flow is:

                                    React
                                      │
                                      ▼
                                    Chat.Api
                                      │
                                      │ Publish event
                                      ▼
                                    RabbitMQ
                                      │
                                      ▼
                                    Chat.EventProcessor

After Chat.EventProcessor finishes processing a message, the React client may need to be notified via SignalR. However, the SignalR Hub belongs to Chat.Api. Chat.EventProcessor should not directly reference Chat.Api or instantiate/call its SignalR Hub. Doing so would create an undesirable service dependency and tightly couple the event processor to the real-time communication implementation.

### Decision

Chat.EventProcessor will publish a new application/integration event to RabbitMQ when processing is completed. Chat.Api will subscribe to this event and use its SignalR Hub to notify the appropriate connected clients.

The resulting flow is:

                        React
                          │
                          │ Request
                          ▼
                        Chat.Api
                          │
                          │ MessageCreated
                          ▼
                        RabbitMQ
                          │
                          ▼
                        Chat.EventProcessor
                          │
                          │ Process / Persist
                          │
                          │ MessageProcessed
                          ▼
                        RabbitMQ
                          │
                          ▼
                        Chat.Api
                          │
                          │ SignalR
                          ▼
                        React

For example, Chat.EventProcessor may publish:

```csharp
public record MessageProcessed(
    Guid MessageId,
    Guid ConversationId,
    Guid SenderId,
    DateTimeOffset ProcessedAt);
```

Chat.Api consumes the event:

```csharp
public class MessageProcessedConsumer
{
        private readonly IHubContext<ChatHub> _hubContext;
        public MessageProcessedConsumer(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }
    
        public async Task Consume(MessageProcessed message)
        {
            await _hubContext.Clients
            .User(message.SenderId.ToString())
            .SendAsync("MessageProcessed", message);
        }
}
```

The important distinction is that Chat.Api uses IHubContext<ChatHub> rather than requiring the event processor to have access to the Hub itself.

### Service Responsibilities

#### Chat.Api

Responsible for:

* HTTP/API endpoints.
* SignalR Hub.
* Managing real-time client communication.
* Translating application events into SignalR notifications.
* Subscribing to events that require client notification.

Chat.EventProcessor

Responsible for:

* Consuming processing commands/events.
* Performing asynchronous processing.
* Persisting or updating application state as required.
* Publishing the resulting application/integration events.

It has no dependency on SignalR or Chat.Api.

**RabbitMQ**

Responsible for:

* Transporting application/integration events between services.
* Decoupling Chat.Api from Chat.EventProcessor.

**SignalR**

Responsible for:

* Real-time communication between Chat.Api and connected clients.

**Consequences**

Pros:

* Chat.EventProcessor remains independent of SignalR.
* Chat.EventProcessor does not need a direct dependency on Chat.Api.
* Chat.Api remains the owner of the SignalR infrastructure.
* RabbitMQ provides a natural asynchronous boundary between services.
* The architecture remains compatible with multiple Chat.Api Kubernetes instances.
* New consumers can subscribe to MessageProcessed without modifying Chat.EventProcessor.

Cons:

* Adds another RabbitMQ event to the system.
* Introduces eventual consistency between processing and client notification.
* Chat.Api must maintain consumers for events that need to reach clients.
* The system must handle duplicate events and failed message processing using normal messaging reliability patterns.

**Idempotency**

Consumers should assume that RabbitMQ messages can be delivered more than once. Chat.Api should therefore make client notifications safe to repeat, or otherwise use an appropriate idempotency mechanism where duplicate notifications would be problematic. The client should also treat the server/database as the source of truth rather than assuming that receiving a SignalR event means the event was received exactly once. Relationship With SignalR Scale-Out When multiple Chat.Api instances are running, the event can be consumed by one of the instances:

                         RabbitMQ
                            │
                    MessageProcessed
                            │
                     ┌──────▼──────┐
                     │   Chat.Api  │
                     │   Pod A     │
                     └──────┬──────┘
                            │
                          SignalR
                            │
                          Redis
                            │
                ┌───────────┴───────────┐
                ▼                       ▼
             Pod B                    Pod C
                │                       │
                ▼                       ▼
           React Client            React Client

Redis provides the SignalR scale-out mechanism described in Use _Redis as the SignalR Scale-Out Backplane_ adr, allowing the Chat.Api instance consuming the RabbitMQ event to notify a client connected to another Chat.Api instance.

### Alternatives Considered

**EventProcessor directly calls Chat.Api**

Rejected.

This would create synchronous coupling between the services and require Chat.EventProcessor to know about the API’s location, authentication, SignalR implementation, and availability.

**EventProcessor hosts a SignalR client**

Rejected.

This would require Chat.EventProcessor to maintain a persistent SignalR connection to Chat.Api, adding unnecessary connection management and coupling.

**EventProcessor directly references Chat.Api’s Hub**

Rejected.

A SignalR Hub is an infrastructure/interface component owned by Chat.Api. Sharing or directly invoking it from another service violates the service boundary.

**EventProcessor publishes directly to Redis**

Rejected.

This would couple Chat.EventProcessor to the SignalR infrastructure and Redis-specific implementation details. RabbitMQ remains the service-to-service messaging mechanism, while Chat.Api owns SignalR.

## Decision Summary

Chat.EventProcessor will never directly access or invoke Chat.Api’s SignalR Hub.

Instead:

                            Chat.EventProcessor
                                │
                                │ publishes application event
                                ▼
                            RabbitMQ
                                │
                                ▼
                            Chat.Api
                                │
                                │ IHubContext<ChatHub>
                                ▼
                            SignalR
                                │
                                ▼
                            React Client

This preserves a clean separation between asynchronous business processing and real-time client communication.