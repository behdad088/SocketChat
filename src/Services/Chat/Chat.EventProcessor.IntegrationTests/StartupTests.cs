using System.Net;
using System.Text.Json;
using Chat.EventProcessor.IntegrationTests.Infrastructure;
using Npgsql;

namespace Chat.EventProcessor.IntegrationTests;

[Collection(EventProcessorCollection.Name)]
public class StartupTests(EventProcessorFixture fixture)
{
    [Fact]
    public async Task Startup_CreatesChatSchema_AndReportsPostgresHealthy()
    {
        using var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/hc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("entries").GetProperty("postgres").GetProperty("status").GetString()
            .ShouldBe("Healthy");

        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select to_regclass('public.mt_doc_profiledocument')::text", connection);
        (await command.ExecuteScalarAsync()).ShouldBe("mt_doc_profiledocument");
    }
}
