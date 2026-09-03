using System.Text;
using RabbitMQ.Client;

namespace Identity.API.Tests.Infrastructure;

internal sealed class RabbitMqTestConsumer : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queue;
    
    private RabbitMqTestConsumer(
        IConnection connection,
        IChannel channel,
        string queue)
    {
        _connection = connection;
        _channel = channel;
        _queue = queue;
    }

    public static async Task<RabbitMqTestConsumer> CreateAsync(
        string amqpUri,
        string exchange)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(amqpUri)
        };

        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(
            exchange,
            ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true);

        await channel.QueueBindAsync(
            queue.QueueName,
            exchange,
            routingKey: string.Empty);

        return new RabbitMqTestConsumer(
            connection,
            channel,
            queue.QueueName);
    }

    public async Task<JsonElement?> WaitForMessageAsync(
        TimeSpan timeout,
        bool autoAck = true)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _channel.BasicGetAsync(_queue, autoAck: autoAck);
            if (result is not null)
            {
                var json = Encoding.UTF8.GetString(result.Body.ToArray());
                return JsonDocument.Parse(json).RootElement.Clone();
            }

            await Task.Delay(100);
        }

        return null;
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _channel.DisposeAsync();
    }
}
