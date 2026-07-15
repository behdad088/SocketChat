using System.Diagnostics;
using Identity.API.Messaging.CloudEvents;
using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;
using MassTransit;
using Microsoft.Extensions.Options;
using Shared.OpenTelemetry;

namespace Identity.API.Messaging.Publishing;

public sealed class EventPublisher<TEvent>(
    IBus bus,
    Telemetry telemetry,
    IOptions<OutboxOptions> outboxOptions,
    ILogger<EventPublisher<TEvent>> logger) : IEventPublisher<TEvent>
    where TEvent : class, IIntegrationEvent
{
    public async Task PublishAsync(OutboxMessage message, TEvent @event, CancellationToken cancellationToken)
    {
        var cloudEvent = CloudEventFactory.Create(@event, message.Id, message.OccurredAt);
        using var activity = telemetry.Tracing.StartCloudEventPublishActivity(cloudEvent, message.TraceParent);
        
        // Create a linked cancellation token source that will cancel after the configured timeout
        // instead of using the default cancellation token directly.
        // This ensures that the publish operation does not hang indefinitely.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(outboxOptions.Value.PublishTimeoutSeconds));

        try
        {
            await bus.Publish(cloudEvent, timeoutCts.Token);
            logger.LogInformation(
                "Published {EventType} for outbox message {MessageId}",
                TEvent.CloudEventType, message.Id);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }
}
