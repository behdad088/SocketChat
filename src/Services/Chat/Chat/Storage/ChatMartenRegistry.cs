using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage;

public class ChatMartenRegistry : MartenRegistry
{
    public ChatMartenRegistry()
    {
        For<ProfileDocument>()
            .UseOptimisticConcurrency(true);
        For<UserChannelDocument>()
            .UseNumericRevisions(true)
            .Metadata(metadata => metadata.Revision.MapTo(doc => doc.Version))
            .Index(
                doc => new { doc.UserId, doc.IsPinned, doc.LastMessageId },
                index => index.Name = "mt_doc_userchanneldocument_idx_chat_list");

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
