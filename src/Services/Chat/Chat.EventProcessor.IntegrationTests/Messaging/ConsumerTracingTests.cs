using System.Collections.Concurrent;
using System.Diagnostics;
using Chat.EventProcessor.IntegrationTests.Infrastructure;
using static Chat.EventProcessor.IntegrationTests.Infrastructure.IdentityEventPublisher;

namespace Chat.EventProcessor.IntegrationTests.Messaging;

[Collection(EventProcessorCollection.Name)]
public sealed class ConsumerTracingTests : IDisposable
{
    private readonly EventProcessorFixture _fixture;
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;

    public ConsumerTracingTests(EventProcessorFixture fixture)
    {
        _fixture = fixture;
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "chat.eventprocessor",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _stopped.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public async Task Consume_ContinuesTheTraceFromTheCloudEvent()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var messageId = await _fixture.Identity.PublishUserCreatedAsync(
            UserEventData.New(version: 0), traceParent: $"00-{traceId}-{spanId}-01");
        await _fixture.Consumed.WaitAsync(messageId);

        var activity = ProcessActivity(messageId);
        activity.DisplayName.ShouldBe("process identity.user.created");
        activity.Kind.ShouldBe(ActivityKind.Consumer);
        activity.TraceId.ShouldBe(traceId);
        activity.ParentSpanId.ShouldBe(spanId);
        activity.Status.ShouldBe(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task Consume_OfAnInvalidEvent_MarksItsSpanAsFailed()
    {
        var messageId = await _fixture.Identity.PublishAsync(
            UserCreatedExchange, UserCreatedType, UserEventData.New(version: 0) with { Email = "" });
        (await _fixture.Identity.IsInErrorQueueAsync("chat.identity.user.created", messageId)).ShouldBeTrue();

        ProcessActivity(messageId).Status.ShouldBe(ActivityStatusCode.Error);
    }

    private Activity ProcessActivity(Guid messageId) =>
        _stopped.Single(activity => (string?)activity.GetTagItem("cloudevents.event_id") == messageId.ToString());

    public void Dispose() => _listener.Dispose();
}
