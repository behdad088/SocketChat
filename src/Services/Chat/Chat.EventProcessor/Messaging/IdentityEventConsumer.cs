using Chat.EventProcessor.Messaging.Identity;
using FluentValidation;
using Marten;
using MassTransit;

namespace Chat.EventProcessor.Messaging;

public sealed class IdentityEventConsumer<TEvent>(
    IDocumentSession session,
    IValidator<TEvent> validator,
    IIdentityEventHandler<TEvent> handler,
    ILogger<IdentityEventConsumer<TEvent>> logger) : IConsumer<CloudEvent<TEvent>>
    where TEvent : class, IIdentityEvent
{
    public async Task Consume(ConsumeContext<CloudEvent<TEvent>> context)
    {
        var cloudEvent = context.Message;
        if (cloudEvent.Type != TEvent.CloudEventType)
            throw new ValidationException(
                $"CloudEvent {cloudEvent.Id} has type '{cloudEvent.Type}', expected '{TEvent.CloudEventType}'.");

        var @event = cloudEvent.Data
                     ?? throw new ValidationException($"CloudEvent {cloudEvent.Id} has no data.");
        await validator.ValidateAndThrowAsync(@event, context.CancellationToken);

        var applied = await handler.Apply(session, @event, context.CancellationToken);
        await session.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "{Outcome} {CloudEventType} {CloudEventId} for user {UserId} at version {Version}",
            applied ? "Applied" : "Ignored",
            TEvent.CloudEventType,
            cloudEvent.Id,
            @event.Id,
            @event.Version);
    }
}
