using Chat.Storage.Commands;
using Chat.Storage.Documents;
using Marten;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Chat.EventProcessor.IntegrationTests.Infrastructure;

public class EventProcessorFixture : IAsyncLifetime
{
    private const ushort RabbitMqPort = 5672;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("chat_eventprocessor_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-alpine")
        .WithUsername("rabbitmq")
        .WithPassword("rabbitmq")
        .Build();

    public WebApplicationFactory<Chat.EventProcessor.Program> Factory { get; private set; } = null!;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public IdentityEventPublisher Identity { get; private set; } = null!;

    public ConsumedMessages Consumed { get; } = new();

    public WriteConflicts Conflicts { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        Environment.SetEnvironmentVariable("ConnectionStrings__ChatDB", PostgresConnectionString);
        Environment.SetEnvironmentVariable(
            "RabbitMQ__Uri", $"rabbitmq://{_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(RabbitMqPort)}");
        Environment.SetEnvironmentVariable("RabbitMQ__Username", "rabbitmq");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "rabbitmq");

        Factory = new WebApplicationFactory<Chat.EventProcessor.Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddConsumeObserver(_ => Consumed);
                services.AddSingleton<UpsertProfile>(provider => Conflicts.Wrap(
                    provider.GetRequiredService<UpsertProfileCommand>().Execute,
                    provider.GetRequiredService<IDocumentStore>()));
            }));

        // Starts the host, which binds the queues before any test publishes.
        _ = Factory.Services;

        Identity = await IdentityEventPublisher.CreateAsync(_rabbitMq.GetConnectionString());
    }

    public async Task<ProfileDocument?> LoadProfileAsync(string userId)
    {
        await using var session = Factory.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return await session.LoadAsync<ProfileDocument>(userId);
    }

    public async Task DisposeAsync()
    {
        await Identity.DisposeAsync();
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}
