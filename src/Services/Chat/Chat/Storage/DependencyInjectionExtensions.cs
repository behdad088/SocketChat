using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Weasel.Core;

namespace Chat.Storage;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddChatStorage(this IServiceCollection services, string connectionString)
    {
        services
            .AddMarten(options => ConfigureMarten(options, connectionString))
            .UseLightweightSessions()
            .ApplyAllDatabaseChangesOnStartup();

        return services;
    }

    public static void ConfigureMarten(StoreOptions options, string connectionString)
    {
        options.Connection(connectionString);
        options.UseSystemTextJsonForSerialization(EnumStorage.AsString);
        options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        options.Schema.Include<ChatMartenRegistry>();
    }
}
