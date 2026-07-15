using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Identity.API.Tests.Infrastructure;

internal sealed class RabbitMqTestConsumer : IDisposable
{
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _queue;

    public RabbitMqTestConsumer(string amqpUri, string exchange)
    {
        var factory = new ConnectionFactory { Uri = new Uri(amqpUri) };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false)
            .GetAwaiter().GetResult();
        _queue = _channel
            .QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true)
            .GetAwaiter().GetResult().QueueName;
        _channel.QueueBindAsync(_queue, exchange, routingKey: string.Empty).GetAwaiter().GetResult();
    }

    public async Task<JsonElement?> WaitForMessageAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await _channel.BasicGetAsync(_queue, autoAck: true);
            if (result is not null)
            {
                var json = Encoding.UTF8.GetString(result.Body.ToArray());
                return JsonDocument.Parse(json).RootElement.Clone();
            }

            await Task.Delay(100);
        }

        return null;
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
