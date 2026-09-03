namespace Chat.Api.Features.GetConversation;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/chat/conversation/{RecipientId}");
        DontCatchExceptions();
        Version(1);
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.NotFoundAsync(ct);
    }
}