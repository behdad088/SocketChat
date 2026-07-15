using System.Diagnostics;
using Identity.API.Messaging.CloudEvents;
using Identity.API.Messaging.Events;
using Identity.API.Messaging.Publishing;

namespace Identity.API.Tests.UnitTests;

public class PublisherTelemetryTests : IDisposable
{
    private readonly ActivitySource _source = new("publisher-telemetry-tests");
    private readonly ActivityListener _listener;

    public PublisherTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == "publisher-telemetry-tests",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private static CloudEvent<UserCreatedEvent> SampleCloudEvent()
    {
        var @event = new UserCreatedEvent(
            "user-1", "ada@example.com", "ada@example.com", "Ada", "Lovelace",
            "", false, 0, DateTimeOffset.UtcNow);
        return CloudEventFactory.Create(@event, Guid.NewGuid(), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Starts_producer_activity_with_messaging_and_cloudevents_tags()
    {
        var cloudEvent = SampleCloudEvent();

        using var activity = _source.StartCloudEventPublishActivity(cloudEvent, originatingTraceParent: null);

        activity.ShouldNotBeNull();
        activity!.Kind.ShouldBe(ActivityKind.Producer);
        activity.DisplayName.ShouldBe("publish identity.user.created");
        activity.GetTagItem("messaging.system").ShouldBe("rabbitmq");
        activity.GetTagItem("messaging.destination.name").ShouldBe("identity.user.created");
        activity.GetTagItem("messaging.operation.type").ShouldBe("send");
        activity.GetTagItem("cloudevents.event_id").ShouldBe(cloudEvent.Id);
        activity.GetTagItem("cloudevents.event_type").ShouldBe("com.socketchat.identity.user.created");
        activity.GetTagItem("cloudevents.event_source").ShouldBe("urn:socketchat:identity-api");
        activity.GetTagItem("cloudevents.event_spec_version").ShouldBe("1.0");
    }

    [Fact]
    public void Injects_w3c_trace_context_into_the_cloudevent()
    {
        var cloudEvent = SampleCloudEvent();

        using var activity = _source.StartCloudEventPublishActivity(cloudEvent, originatingTraceParent: null);

        cloudEvent.TraceParent.ShouldNotBeNullOrEmpty();
        cloudEvent.TraceParent!.ShouldContain(activity!.TraceId.ToHexString());
    }

    [Fact]
    public void Links_to_the_originating_request_trace_when_traceparent_is_stored()
    {
        var cloudEvent = SampleCloudEvent();
        const string storedTraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        using var activity = _source.StartCloudEventPublishActivity(cloudEvent, storedTraceParent);

        activity.ShouldNotBeNull();
        activity!.Links.ShouldContain(l =>
            l.Context.TraceId.ToHexString() == "0af7651916cd43dd8448eb211c80319c");
    }

    [Fact]
    public void Ignores_invalid_stored_traceparent()
    {
        var cloudEvent = SampleCloudEvent();

        using var activity = _source.StartCloudEventPublishActivity(cloudEvent, "not-a-traceparent");

        activity.ShouldNotBeNull();
        activity!.Links.ShouldBeEmpty();
    }
}
