namespace Chat.API.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class ChatApiCollection : ICollectionFixture<ChatApiFixture>
{
    public const string Name = "ChatApi";
}
