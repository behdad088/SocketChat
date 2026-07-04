using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Identity.API.Tests.Infrastructure;

public class WebApiContainerFactory : IAsyncLifetime
{
    private const ushort PostgresPort = 5432;

    private readonly IContainer _postgres = new ContainerBuilder()
        .WithImage("postgres:16-alpine")
        .WithPortBinding(PostgresPort, true)
        .WithEnvironment("POSTGRES_USER", "postgres")
        .WithEnvironment("POSTGRES_PASSWORD", "postgres")
        .WithEnvironment("POSTGRES_DB", "identity_test")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(PostgresPort))
        .Build();

    internal string PostgresConnectionString =>
        $"Host={_postgres.Hostname};Port={_postgres.GetMappedPublicPort(PostgresPort)};Database=identity_test;Username=postgres;Password=postgres";

    
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}