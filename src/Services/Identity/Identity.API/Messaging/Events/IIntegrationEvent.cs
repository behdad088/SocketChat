namespace Identity.API.Messaging.Events;

/// <summary>
/// Marker for events published to RabbitMQ. The metadata defines the
/// CloudEvents fields and the MassTransit exchange configuration. To add a
/// new event, create a new record and register it with AddEventPublisher().
/// </summary>
public interface IIntegrationEvent
{
    static abstract string CloudEventType { get; }
    static abstract string ExchangeName { get; }
}

public static class EventConstants
{
    public const string UserCreatedCloudEventType = "com.socketchat.identity.user.created";
    public const string UserCreatedExchangeName = "identity.user.created";
    
    public const string UserUpdatedCloudEventType = "com.socketchat.identity.user.updated";
    public const string UserUpdatedExchangeName = "identity.user.updated";
}
