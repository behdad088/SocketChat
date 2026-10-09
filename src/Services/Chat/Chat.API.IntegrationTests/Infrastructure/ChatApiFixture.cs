using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Chat.API.IntegrationTests.Infrastructure;

public class ChatApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        // AWS's mirror of the Docker Official Image: Docker Hub rate-limits anonymous pulls from CI runners.
        .WithImage("public.ecr.aws/docker/library/postgres:16-alpine")
        .WithDatabase("chat_api_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public WebApplicationFactory<Chat.Api.Program> Factory { get; private set; } = null!;

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__ChatDB", PostgresConnectionString);
        Factory = new WebApplicationFactory<Chat.Api.Program>();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
