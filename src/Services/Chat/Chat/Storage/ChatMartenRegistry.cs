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

        For<MessageDocument>()
            .Index(
                doc => new { doc.ConversationId, doc.Id },
                index => index.Name = "mt_doc_messagedocument_idx_conversation_id_id")
            .Index(
                doc => new { doc.ConversationId, doc.RevisionId },
                index => index.Name = "mt_doc_messagedocument_idx_conversation_id_revision_id");

        For<MessageVersionDocument>();
    }
}
