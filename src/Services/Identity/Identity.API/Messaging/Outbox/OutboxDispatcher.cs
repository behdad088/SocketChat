using System.Diagnostics.Metrics;
using Identity.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.OpenTelemetry;
using Serilog;

namespace Identity.API.Messaging.Outbox;

public sealed class OutboxDispatcher : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxEventRegistry _outboxEventRegistry;
    private readonly Telemetry _telemetry;
    private readonly ILogger<OutboxDispatcher> _logger;
    private readonly OutboxOptions _options;
    private readonly Counter<long> _dispatchedCounter;
    private readonly Counter<long> _failedCounter;
    private long _pendingDepth;

    public OutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        OutboxEventRegistry outboxEventRegistry,
        Telemetry telemetry,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _outboxEventRegistry = outboxEventRegistry;
        _telemetry = telemetry;
        _logger = logger;
        _options = options.Value;
        _dispatchedCounter = telemetry.Metrics.CreateCounter<long>("outbox.messages.dispatched");
        _failedCounter = telemetry.Metrics.CreateCounter<long>("outbox.messages.failed");
        telemetry.Metrics.CreateObservableGauge(
            "outbox.messages.pending", () => Volatile.Read(ref _pendingDepth));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        var lastCleanup = DateTimeOffset.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);

                if (DateTimeOffset.UtcNow - lastCleanup > CleanupInterval)
                {
                    await CleanupDispatchedAsync(stoppingToken);
                    lastCleanup = DateTimeOffset.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatch cycle failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pendingOutboxMessages = await dbContext.OutboxMessages
            .Where(m => m.DispatchedAt == null && m.NextAttemptAt <= now)
            .OrderBy(m => m.OccurredAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        Volatile.Write(ref _pendingDepth,
            await dbContext.OutboxMessages.CountAsync(m => m.DispatchedAt == null, cancellationToken));

        if (pendingOutboxMessages.Count == 0)
        {
            return;
        }

        using var activity = _telemetry.Tracing.StartActivity("outbox dispatch cycle");
        activity?.SetTag("outbox.batch.size", pendingOutboxMessages.Count);

        var shuttingDown = false;

        foreach (var message in pendingOutboxMessages)
        {
            var published = false;

            if (_outboxEventRegistry.TryGetDispatcher(message.EventType, out var dispatch))
            {
                try
                {
                    await dispatch(scope.ServiceProvider, message, cancellationToken);
                    published = true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    shuttingDown = true;
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispatch outbox message {MessageId} ({EventType})",
                        message.Id, message.EventType);
                    message.LastError = ex.Message;
                }
            }
            else
            {
                // This should only happen if the event type was removed from the registry
                // (e.g. a feature was disabled) after the message was written to the outbox.
                message.LastError = $"No dispatcher registered for event type '{message.EventType}'.";
                _logger.LogError("No dispatcher registered for outbox event type {EventType} (message {MessageId})",
                    message.EventType, message.Id);
            }

            if (published)
            {
                message.DispatchedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                _dispatchedCounter.Add(1, new KeyValuePair<string, object?>("event.type", message.EventType));
            }
            else
            {
                message.AttemptCount++;
                message.NextAttemptAt = DateTimeOffset.UtcNow + OutboxRetryPolicy.NextDelay(message.AttemptCount);
                _failedCounter.Add(1, new KeyValuePair<string, object?>("event.type", message.EventType));

                if (message.AttemptCount >= 10)
                {
                    _logger.LogWarning(
                        "Outbox message {MessageId} ({EventType}) has failed {AttemptCount} dispatch attempts",
                        message.Id, message.EventType, message.AttemptCount);
                }
            }
        }

        await dbContext.SaveChangesAsync(shuttingDown ? CancellationToken.None : cancellationToken);
    }

    private async Task CleanupDispatchedAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
        var deleted = await dbContext.OutboxMessages
            .Where(m => m.DispatchedAt != null && m.DispatchedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Outbox cleanup deleted {Count} dispatched messages older than {Cutoff}",
                deleted, cutoff);
        }
    }
}
