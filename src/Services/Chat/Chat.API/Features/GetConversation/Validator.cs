using Shared;

namespace Chat.Api.Features.GetConversation;

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.RecipientId).MustBeValidGuid();
    }
}