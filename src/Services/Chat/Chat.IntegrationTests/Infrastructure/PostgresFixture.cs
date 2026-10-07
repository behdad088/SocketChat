using Chat.Storage;
using Marten;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Chat.IntegrationTests.Infrastructure;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("chat_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public IDocumentStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        Store = DocumentStore.For(options =>
            DependencyInjectionExtensions.ConfigureMarten(options, _postgres.GetConnectionString()));

        await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        Store.Dispose();
        await _postgres.DisposeAsync();
    }

    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var databaseName = $"chat_{Guid.NewGuid():N}";
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"create database {databaseName}", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = databaseName }
            .ConnectionString;
    }

    public Task<IReadOnlyList<string>> GetTableNamesAsync() =>
        QueryStringsAsync("select table_name from information_schema.tables where table_schema = 'public'");

    public Task<IReadOnlyList<string>> GetJsonKeysAsync(string tableName, string id) =>
        QueryStringsAsync($"select jsonb_object_keys(data) from public.{tableName} where id = $1", id);

    private async Task<IReadOnlyList<string>> QueryStringsAsync(string sql, params object[] parameters)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters)
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            values.Add(reader.IsDBNull(0) ? null! : reader.GetString(0));

        return values;
    }
}
