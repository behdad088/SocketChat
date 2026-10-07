using Chat.IntegrationTests.Infrastructure;
using Chat.Storage;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class ConcurrentMigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task HostsStartingTogether_AgainstAnEmptyDatabase_AllStartAndCreateTheSchema()
    {
        var connectionString = await fixture.CreateEmptyDatabaseAsync();
        var hosts = Enumerable.Range(0, 3).Select(_ => BuildHost(connectionString)).ToList();

        try
        {
            await Task.WhenAll(hosts.Select(host => host.StartAsync()));

            var store = hosts[0].Services.GetRequiredService<IDocumentStore>();
            await store.Storage.Database.AssertDatabaseMatchesConfigurationAsync();
        }
        finally
        {
            foreach (var host in hosts)
                host.Dispose();
        }
    }

    private static IHost BuildHost(string connectionString)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddChatStorage(connectionString);
        return builder.Build();
    }
}
