namespace Identity.API.Messaging.Outbox;

public class OutboxMessage
{
    /// <summary>Also used as the CloudEvent id; stable across publish retries.</summary>
    public Guid Id { get; set; }

    /// <summary>The CloudEvent type string, e.g. "com.socketchat.identity.user.created".</summary>
    public required string EventType { get; set; }

    /// <summary>The serialized event record (snake_case JSON), stored as jsonb.</summary>
    public required string Payload { get; set; }

    /// <summary>W3C traceparent of the originating request, for span links.</summary>
    public string? TraceParent { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Null = pending dispatch.</summary>
    public DateTimeOffset? DispatchedAt { get; set; }

    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}
