using System.ComponentModel.DataAnnotations;

namespace Chat.EventProcessor.Configurations;

internal sealed record LoggerConfigurations
{
    [ConfigurationKeyName("Logger:elasticsearch")]
    [Required]
    public required string ElasticSearch { get; init; }
}
