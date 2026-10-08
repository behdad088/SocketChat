using System.Diagnostics;
using Chat.EventProcessor.Messaging.Identity;
using FluentValidation;
using Marten;
using MassTransit;
using Shared.OpenTelemetry;

namespace Chat.EventProcessor.Messaging;

public sealed class IdentityEventConsumer<TEvent>(
    IDocumentSession session,
    IValidator<TEvent> validator,
    IIdentityEventHandler<TEvent> handler,
    Telemetry telemetry,
    ILogger<IdentityEventConsumer<TEvent>> logger) : IConsumer<CloudEvent<TEvent>>
    where TEvent : class, IIdentityEvent
{
    public async Task Consume(ConsumeContext<CloudEvent<TEvent>> context)
    {
        var cloudEvent = context.Message;
        using var activity = telemetry.Tracing.StartCloudEventProcessActivity(cloudEvent, TEvent.ExchangeName);

        try
        {
            await ProcessAsync(cloudEvent, context.CancellationToken);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);
            throw;
        }
    }

    private async Task ProcessAsync(CloudEvent<TEvent> cloudEvent, CancellationToken ct)
    {
        if (cloudEvent.Type != TEvent.CloudEventType)
            throw new ValidationException(
                $"CloudEvent {cloudEvent.Id} has type '{cloudEvent.Type}', expected '{TEvent.CloudEventType}'.");

        var @event = cloudEvent.Data
                     ?? throw new ValidationException($"CloudEvent {cloudEvent.Id} has no data.");
        await validator.ValidateAndThrowAsync(@event, ct);

        var applied = await handler.Apply(session, @event, ct);
        await session.SaveChangesAsync(ct);

        logger.LogInformation(
            "{Outcome} {CloudEventType} {CloudEventId} for user {UserId} at version {Version}",
            applied ? "Applied" : "Ignored",
            TEvent.CloudEventType,
            cloudEvent.Id,
            @event.Id,
            @event.Version);
    }
}
