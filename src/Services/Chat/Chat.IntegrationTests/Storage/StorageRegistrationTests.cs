using Chat.Storage;
using Chat.Storage.Commands;
using Chat.Storage.Queries;
using Microsoft.Extensions.DependencyInjection;

namespace Chat.IntegrationTests.Storage;

public class StorageRegistrationTests
{
    [Theory]
    [InlineData(typeof(UpsertProfile))]
    [InlineData(typeof(SetLastOnline))]
    [InlineData(typeof(DeleteUserData))]
    [InlineData(typeof(GetProfile))]
    [InlineData(typeof(GetProfiles))]
    [InlineData(typeof(StartConversation))]
    [InlineData(typeof(GetChannel))]
    [InlineData(typeof(GetConversation))]
    [InlineData(typeof(SetChannelPreferences))]
    [InlineData(typeof(ListUserChannels))]
    [InlineData(typeof(MarkChannelRead))]
    public void AddChatStorage_RegistersTheStorageOperation(Type operation)
    {
        using var provider = new ServiceCollection()
            .AddChatStorage("Host=localhost;Database=unused")
            .BuildServiceProvider();

        provider.GetRequiredService(operation).ShouldNotBeNull();
    }
}
