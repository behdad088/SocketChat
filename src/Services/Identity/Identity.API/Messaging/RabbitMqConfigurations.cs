using System.ComponentModel.DataAnnotations;

namespace Identity.API.Messaging;

public sealed record RabbitMqConfigurations
{
    [ConfigurationKeyName("RabbitMQ:Uri")]
    [Required]
    public required string Uri { get; init; }

    [ConfigurationKeyName("RabbitMQ:Username")]
    [Required]
    public required string Username { get; init; }

    [ConfigurationKeyName("RabbitMQ:Password")]
    [Required]
    public required string Password { get; init; }
}
