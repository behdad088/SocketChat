using System.ComponentModel.DataAnnotations;

namespace Identity.API.Configurations.ConfigurationOptions;

internal sealed record LoggerConfigurations
{
    [ConfigurationKeyName("Logger:elasticsearch")]
    [Required]
    public required string ElasticSearch { get; init; }
}