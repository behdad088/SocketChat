using Chat.Storage.Commands;
using Chat.Storage.Queries;
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

        
        services.AddSingleton<UpsertProfileCommand>();
        services.AddSingleton<UpsertProfile>(sp => sp.GetRequiredService<UpsertProfileCommand>().Execute);
        
        services.AddSingleton<SetLastOnlineCommand>();
        services.AddSingleton<SetLastOnline>(sp => sp.GetRequiredService<SetLastOnlineCommand>().Execute);
        
        services.AddSingleton<GetProfileQuery>();
        services.AddSingleton<GetProfile>(sp =>  sp.GetRequiredService<GetProfileQuery>().Execute);
        
        services.AddSingleton<GetProfilesQuery>();
        services.AddSingleton<GetProfiles>(sp => sp.GetRequiredService<GetProfilesQuery>().Execute);

        services.AddSingleton<DeleteUserDataCommand>();
        services.AddSingleton<DeleteUserData>(sp => sp.GetRequiredService<DeleteUserDataCommand>().Execute);

        services.AddSingleton<StartConversationCommand>();
        services.AddSingleton<StartConversation>(sp => sp.GetRequiredService<StartConversationCommand>().Execute);

        services.AddSingleton<GetChannelQuery>();
        services.AddSingleton<GetChannel>(sp => sp.GetRequiredService<GetChannelQuery>().Execute);

        services.AddSingleton<GetConversationQuery>();
        services.AddSingleton<GetConversation>(sp => sp.GetRequiredService<GetConversationQuery>().Execute);
        
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
