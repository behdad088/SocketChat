using System.ComponentModel.DataAnnotations;

namespace Identity.API.Messaging.Outbox;

public sealed record OutboxOptions
{
    [ConfigurationKeyName("Outbox:PollIntervalSeconds")]
    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 2;

    [ConfigurationKeyName("Outbox:BatchSize")]
    [Range(1, 1000)]
    public int BatchSize { get; init; } = 20;

    [ConfigurationKeyName("Outbox:PublishTimeoutSeconds")]
    [Range(1, 300)]
    public int PublishTimeoutSeconds { get; init; } = 10;

    [ConfigurationKeyName("Outbox:RetentionDays")]
    [Range(1, 365)]
    public int RetentionDays { get; init; } = 7;
}
