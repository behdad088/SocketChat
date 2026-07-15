using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;
using Identity.API.Messaging.Publishing;
using MassTransit;
using Shared.Configurations;

namespace Identity.API.Messaging;

public static class DependencyInjectionExtensions
{
    public static void AddEventPublishing(this IServiceCollection services, ConfigurationManager configuration)
    {
        services.TrySetConfiguration<RabbitMqConfigurations>(configuration, out var rabbitMqConfigurations);
        services.TrySetConfiguration<OutboxOptions>(configuration, out _);

        var registry = new OutboxEventRegistry();
        registry.Add<UserCreatedEvent>();
        registry.Add<UserUpdatedEvent>();
        services.AddSingleton(registry);

        services.AddScoped(typeof(IEventPublisher<>), typeof(EventPublisher<>));
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddHostedService<OutboxDispatcher>();

        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConfigurations.Uri), h =>
                {
                    h.Username(rabbitMqConfigurations.Username);
                    h.Password(rabbitMqConfigurations.Password);
                });

                cfg.ClearSerialization();
                cfg.UseRawJsonSerializer(isDefault: true);

                registry.ConfigureTopology(cfg);
            });
        });
    }
}
