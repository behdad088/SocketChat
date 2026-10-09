using Chat.Storage.Documents;
using Marten;

namespace Chat.Storage.Queries;

public record GetConversationParameters(string ConversationId);

public abstract record GetConversationResult
{
    public record Success(ConversationDocument Conversation) : GetConversationResult;

    public record NotFound : GetConversationResult;
}

public delegate Task<GetConversationResult> GetConversation(
    IQuerySession session,
    GetConversationParameters parameters,
    CancellationToken ct);

public class GetConversationQuery
{
    public async Task<GetConversationResult> Execute(
        IQuerySession session,
        GetConversationParameters parameters,
        CancellationToken ct)
    {
        var conversation = await session.LoadAsync<ConversationDocument>(parameters.ConversationId, ct);

        return conversation is null
            ? new GetConversationResult.NotFound()
            : new GetConversationResult.Success(conversation);
    }
}
