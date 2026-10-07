using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage;

public class ChatMartenRegistry : MartenRegistry
{
    public ChatMartenRegistry()
    {
        For<ProfileDocument>();
        For<UserChannelDocument>()
            .UseNumericRevisions(true)
            .Metadata(metadata => metadata.Revision.MapTo(doc => doc.Version))
            .Index(
                doc => new { doc.UserId, doc.LastMessageAt },
                index => index.Name = "mt_doc_userchanneldocument_idx_user_id_last_message_at");

        For<ConversationDocument>();
    }
}
