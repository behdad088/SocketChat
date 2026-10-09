using System.Diagnostics;

namespace Chat.EventProcessor.Messaging;

public static class ConsumerTelemetryExtensions
{
    public static Activity? StartCloudEventProcessActivity<TEvent>(
        this ActivitySource activitySource,
        CloudEvent<TEvent> cloudEvent,
        string exchangeName) where TEvent : class
    {
        ActivityContext.TryParse(cloudEvent.TraceParent, cloudEvent.TraceState, isRemote: true, out var parentContext);

        return activitySource.StartActivity(
            $"process {exchangeName}",
            ActivityKind.Consumer,
            parentContext,
            new ActivityTagsCollection
            {
                ["messaging.system"] = "rabbitmq",
                ["messaging.destination.name"] = exchangeName,
                ["messaging.operation.type"] = "process",
                ["cloudevents.event_id"] = cloudEvent.Id,
                ["cloudevents.event_type"] = cloudEvent.Type,
                ["cloudevents.event_source"] = cloudEvent.Source,
                ["cloudevents.event_spec_version"] = cloudEvent.SpecVersion
            });
    }
}
