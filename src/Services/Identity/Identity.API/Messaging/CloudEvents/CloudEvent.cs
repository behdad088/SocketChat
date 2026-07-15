using System.Text.Json.Serialization;
using Identity.API.Messaging.Events;

namespace Identity.API.Messaging.CloudEvents;

/// <summary>
/// CloudEvents v1.0 structured-mode envelope. This record matches the JSON
/// sent over RabbitMQ using MassTransit’s raw JSON serializer.
/// </summary>
public sealed record CloudEvent<TEvent> where TEvent : IIntegrationEvent
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("source")] public required string Source { get; init; }
    [JsonPropertyName("specversion")] public required string SpecVersion { get; init; }
    [JsonPropertyName("datacontenttype")] public required string DataContentType { get; init; }
    [JsonPropertyName("time")] public required DateTimeOffset Time { get; init; }
    [JsonPropertyName("data")] public required TEvent Data { get; init; }

    [JsonPropertyName("traceparent")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceParent { get; set; }

    [JsonPropertyName("tracestate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceState { get; set; }
}
