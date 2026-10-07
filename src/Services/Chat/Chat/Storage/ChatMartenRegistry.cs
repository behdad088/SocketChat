using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage;

public class ChatMartenRegistry : MartenRegistry
{
    public ChatMartenRegistry()
    {
        For<ProfileDocument>();
    }
}
