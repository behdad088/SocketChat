using System.Diagnostics;
using Identity.API.Messaging.CloudEvents;
using Identity.API.Messaging.Events;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

namespace Identity.API.Messaging.Publishing;

public static class PublisherTelemetryExtensions
{
    private static readonly TraceContextPropagator Propagator = new();

    /// <summary>
    /// Starts a Producer span for the publish, tagged with OTel messaging +
    /// CloudEvents semantic conventions, linked to the originating request's
    /// trace (the outbox dispatcher runs on a background loop, so without the
    /// link the span would be disconnected from the HTTP request that caused
    /// it), and injects W3C trace context into the CloudEvent so consumers
    /// can continue the trace.
    /// </summary>
    public static Activity? StartCloudEventPublishActivity<TEvent>(
        this ActivitySource activitySource,
        CloudEvent<TEvent> cloudEvent,
        string? originatingTraceParent) where TEvent : IIntegrationEvent
    {
        var links = new List<ActivityLink>();
        if (ActivityContext.TryParse(originatingTraceParent, null, out var originContext))
        {
            links.Add(new ActivityLink(originContext));
        }

        var activity = activitySource.StartActivity(
            $"publish {TEvent.ExchangeName}",
            ActivityKind.Producer,
            default(ActivityContext), // parentContext: falls back to Activity.Current (the dispatch-cycle span)
            CreteActivityTags(cloudEvent),
            links);

        if (activity is not null)
        {
            Propagator.Inject(
                new PropagationContext(activity.Context, Baggage.Current),
                cloudEvent,
                static (envelope, key, value) =>
                {
                    if (key == W3CContextStandard.TraceParent) envelope.TraceParent = value;
                    else if (key == W3CContextStandard.TraceState) envelope.TraceState = value;
                });
        }

        return activity;
    }

    private static ActivityTagsCollection CreteActivityTags<TEvent>(CloudEvent<TEvent> cloudEvent) where TEvent : IIntegrationEvent
    {
        return new ActivityTagsCollection
        {
            [SpanTags.MessagingSystem] = "rabbitmq",
            [SpanTags.MessagingDestinationName] = TEvent.ExchangeName,
            [SpanTags.MessagingOperationType] = "send",
            [SpanTags.CloudEventId] = cloudEvent.Id,
            [SpanTags.CloudEventType] = cloudEvent.Type,
            [SpanTags.CloudEventSource] = cloudEvent.Source,
            [SpanTags.CloudEventSpecVersion] = cloudEvent.SpecVersion
        };
    }

    private static class SpanTags
    {
        public const string CloudEventId = "cloudevents.event_id";
        public const string CloudEventSource = "cloudevents.event_source";
        public const string CloudEventSpecVersion = "cloudevents.event_spec_version";
        public const string CloudEventType = "cloudevents.event_type";
        public const string MessagingSystem = "messaging.system";
        public const string MessagingDestinationName = "messaging.destination.name";
        public const string MessagingOperationType = "messaging.operation.type";
    }
    
    private static class W3CContextStandard
    {
        public const string TraceParent = "traceparent";
        public const string TraceState = "tracestate";
    }
}
