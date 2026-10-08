using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Chat.EventProcessor.IntegrationTests.Infrastructure;

// Publishes CloudEvents the way Identity.API does: raw JSON on a durable fanout exchange.
public sealed class IdentityEventPublisher : IAsyncDisposable
{
    public const string UserCreatedType = "com.socketchat.identity.user.created";
    public const string UserCreatedExchange = "identity.user.created";
    public const string UserUpdatedType = "com.socketchat.identity.user.updated";
    public const string UserUpdatedExchange = "identity.user.updated";
    public const string UserDeletedType = "com.socketchat.identity.user.deleted";
    public const string UserDeletedExchange = "identity.user.deleted";

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    private IdentityEventPublisher(IConnection connection, IChannel channel)
    {
        _connection = connection;
        _channel = channel;
    }

    public static async Task<IdentityEventPublisher> CreateAsync(string amqpUri)
    {
        var connection = await new ConnectionFactory { Uri = new Uri(amqpUri) }.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        return new IdentityEventPublisher(connection, channel);
    }

    public Task<Guid> PublishUserCreatedAsync(UserEventData user) =>
        PublishAsync(UserCreatedExchange, UserCreatedType, user);

    public Task<Guid> PublishUserUpdatedAsync(UserEventData user) =>
        PublishAsync(UserUpdatedExchange, UserUpdatedType, user);

    public Task<Guid> PublishUserDeletedAsync(UserDeletedEventData user) =>
        PublishAsync(UserDeletedExchange, UserDeletedType, user);

    public async Task<Guid> PublishAsync(string exchange, string type, object? data)
    {
        var id = Guid.NewGuid();
        var cloudEvent = new Dictionary<string, object?>
        {
            ["id"] = id.ToString(),
            ["type"] = type,
            ["source"] = "urn:socketchat:identity-api",
            ["specversion"] = "1.0",
            ["datacontenttype"] = "application/json",
            ["time"] = DateTimeOffset.UtcNow,
            ["data"] = data
        };

        await PublishBodyAsync(exchange, id, JsonSerializer.SerializeToUtf8Bytes(cloudEvent));
        return id;
    }

    public async Task<Guid> PublishBodyAsync(string exchange, string body)
    {
        var id = Guid.NewGuid();
        await PublishBodyAsync(exchange, id, Encoding.UTF8.GetBytes(body));
        return id;
    }

    // A message MassTransit dead-letters keeps its body and AMQP message id in `<queue>_error`.
    public async Task<bool> IsInErrorQueueAsync(string queue, Guid messageId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            // BasicGet on a queue that doesn't exist yet closes the channel, so each attempt uses its own.
            await using var channel = await _connection.CreateChannelAsync();
            try
            {
                while (await channel.BasicGetAsync($"{queue}_error", autoAck: true) is { } message)
                {
                    if (message.BasicProperties.MessageId == messageId.ToString())
                        return true;
                }
            }
            catch (OperationInterruptedException)
            {
            }

            await Task.Delay(100);
        }

        return false;
    }

    private async Task PublishBodyAsync(string exchange, Guid id, byte[] body)
    {
        await _channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        await _channel.BasicPublishAsync(
            exchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties { ContentType = "application/json", MessageId = id.ToString() },
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

public sealed record UserEventData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("profile_picture")] string ProfilePicture,
    [property: JsonPropertyName("email_confirmed")] bool EmailConfirmed,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt)
{
    public static UserEventData New(int version) => new(
        Id: Guid.NewGuid().ToString(),
        Email: "test@test.com",
        Username: "test.user",
        Name: "Test",
        LastName: "TestLastname",
        ProfilePicture: "https://test.com/test.png",
        EmailConfirmed: true,
        Version: version,
        OccurredAt: DateTimeOffset.UtcNow);
}

public sealed record UserDeletedEventData(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt);
