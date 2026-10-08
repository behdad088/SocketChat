using System.Text.Json.Serialization;

namespace Chat.EventProcessor.Messaging;

// The CloudEvents envelope Identity publishes as raw JSON. Only the fields Chat reads are mapped.
public sealed record CloudEvent<TEvent> where TEvent : class
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("data")] public TEvent? Data { get; init; }
    [JsonPropertyName("traceparent")] public string? TraceParent { get; init; }
    [JsonPropertyName("tracestate")] public string? TraceState { get; init; }
}
