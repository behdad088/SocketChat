using System.Collections.Concurrent;
using MassTransit;

namespace Chat.EventProcessor.IntegrationTests.Infrastructure;

public sealed class ConsumedMessages : IConsumeObserver
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _consumed = new();

    public Task WaitAsync(Guid messageId) => Get(messageId).Task.WaitAsync(TimeSpan.FromSeconds(30));

    public Task PreConsume<T>(ConsumeContext<T> context) where T : class => Task.CompletedTask;

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        if (context.MessageId is { } messageId)
            Get(messageId).TrySetResult();

        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class => Task.CompletedTask;

    private TaskCompletionSource Get(Guid messageId) =>
        _consumed.GetOrAdd(messageId, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
}
