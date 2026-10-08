using Chat.EventProcessor.Messaging.Identity;
using FluentValidation;
using MassTransit;
using RabbitMQ.Client;
using Shared.Configurations;

namespace Chat.EventProcessor.Messaging;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddIdentityEventConsumers(
        this IServiceCollection services,
        ConfigurationManager configuration)
    {
        services.TrySetConfiguration<RabbitMqConfigurations>(configuration, out var rabbitMqConfigurations);

        services.AddSingleton<IIdentityEventHandler<UserCreatedEvent>, UserCreatedHandler>();
        services.AddSingleton<IIdentityEventHandler<UserUpdatedEvent>, UserUpdatedHandler>();
        services.AddSingleton<IIdentityEventHandler<UserDeletedEvent>, UserDeletedHandler>();

        // Host start waits until the queues are bound; Identity's fanout exchanges drop events that reach
        // them before a queue is bound.
        services.AddOptions<MassTransitHostOptions>().Configure(options => options.WaitUntilStarted = true);

        services.AddMassTransit(x =>
        {
            x.AddConsumer<IdentityEventConsumer<UserCreatedEvent>>();
            x.AddConsumer<IdentityEventConsumer<UserUpdatedEvent>>();
            x.AddConsumer<IdentityEventConsumer<UserDeletedEvent>>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConfigurations.Uri), h =>
                {
                    h.Username(rabbitMqConfigurations.Username);
                    h.Password(rabbitMqConfigurations.Password);
                });

                cfg.ReceiveIdentityEvent<UserCreatedEvent>(context);
                cfg.ReceiveIdentityEvent<UserUpdatedEvent>(context);
                cfg.ReceiveIdentityEvent<UserDeletedEvent>(context);
            });
        });

        return services;
    }

    private static void ReceiveIdentityEvent<TEvent>(
        this IRabbitMqBusFactoryConfigurator cfg,
        IBusRegistrationContext context)
        where TEvent : class, IIdentityEvent
    {
        cfg.ReceiveEndpoint($"chat.{TEvent.ExchangeName}", endpoint =>
        {
            endpoint.ConfigureConsumeTopology = false;
            endpoint.UseRawJsonDeserializer(RawSerializerOptions.AnyMessageType, isDefault: true);
            endpoint.Bind(TEvent.ExchangeName, exchange => exchange.ExchangeType = ExchangeType.Fanout);

            // Each attempt runs in a new scope, so the consumer gets a new IDocumentSession and reloads the
            // profile after a ConcurrencyException or DocumentAlreadyExistsException.
            endpoint.UseMessageRetry(retry =>
            {
                retry.Ignore<ValidationException>();
                retry.Incremental(10, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100));
            });

            endpoint.ConfigureConsumer<IdentityEventConsumer<TEvent>>(context);
        });
    }
}
