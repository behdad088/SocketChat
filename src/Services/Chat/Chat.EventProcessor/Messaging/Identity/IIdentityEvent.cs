using Marten;

namespace Chat.EventProcessor.Messaging.Identity;

public interface IIdentityEvent
{
    static abstract string CloudEventType { get; }
    static abstract string ExchangeName { get; }

    string Id { get; }
    int Version { get; }
}

public interface IIdentityEventHandler<in TEvent> where TEvent : IIdentityEvent
{
    Task<bool> Apply(IDocumentSession session, TEvent @event, CancellationToken ct);
}
