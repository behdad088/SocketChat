using Chat.Storage;
using JasperFx;
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

    // Marten's SaveChangesAsync throws when another session wrote the document after this one
    // loaded it; running the work again in a fresh session reloads it.
    public async Task SaveWithRetryAsync(Func<IDocumentSession, Task> work)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var session = Store.LightweightSession();
                await work(session);
                await session.SaveChangesAsync();
                return;
            }
            catch (Exception exception)
                when (exception is ConcurrencyException or DocumentAlreadyExistsException && attempt < 50)
            {
            }
        }
    }

    public Task<IReadOnlyList<string>> GetTableNamesAsync() =>
        QueryStringsAsync("select table_name from information_schema.tables where table_schema = 'public'");

    public async Task<string?> GetIndexDefinitionAsync(string indexName) =>
        (await QueryStringsAsync(
            "select indexdef from pg_indexes where schemaname = 'public' and indexname = $1",
            indexName)).SingleOrDefault();

    public Task<IReadOnlyList<string>> GetJsonKeysAsync(string tableName, string id) =>
        QueryStringsAsync($"select jsonb_object_keys(data) from public.{tableName} where id = $1", id);

    public Task<IReadOnlyList<string>> GetFirstElementJsonKeysAsync(string tableName, string id, string arrayField) =>
        QueryStringsAsync($"select jsonb_object_keys(data -> $2 -> 0) from public.{tableName} where id = $1", id, arrayField);

    public async Task<string?> GetJsonFieldAsync(string tableName, string id, string field) =>
        (await QueryStringsAsync($"select data ->> $2 from public.{tableName} where id = $1", id, field))
        .SingleOrDefault();

    public async Task<string> ExplainWithoutSeqScanAsync(NpgsqlCommand query)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using (var disableSeqScan = new NpgsqlCommand("set enable_seqscan = off", connection))
            await disableSeqScan.ExecuteNonQueryAsync();

        await using var explain = new NpgsqlCommand("explain " + query.CommandText, connection);
        foreach (NpgsqlParameter parameter in query.Parameters)
            explain.Parameters.Add(parameter.Clone());

        var plan = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            plan.Add(reader.GetString(0));

        return string.Join(Environment.NewLine, plan);
    }

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
