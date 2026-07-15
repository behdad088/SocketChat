using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Testcontainers.RabbitMq;

namespace Identity.API.Tests.Infrastructure;

public class WebApiContainerFactory : IAsyncLifetime
{
    private const ushort PostgresPort = 5432;
    private const ushort RabbitMqPort = 5672;

    private readonly IContainer _postgres = new ContainerBuilder()
        .WithImage("postgres:16-alpine")
        .WithPortBinding(PostgresPort, true)
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .WithEnvironment("POSTGRES_DB", "identity_test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(PostgresPort))
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-alpine")
        .WithUsername("rabbitmq")
        .WithPassword("rabbitmq")
        .Build();

    internal string PostgresConnectionString =>
        $"Host={_postgres.Hostname};Port={_postgres.GetMappedPublicPort(PostgresPort)};Database=identity_test;Username=postgres;Password=postgres";

    internal string RabbitMqUri =>
        $"rabbitmq://{_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(RabbitMqPort)}";

    internal string RabbitMqAmqpUri => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
