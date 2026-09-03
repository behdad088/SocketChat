using System.Text.Json.Serialization;

namespace Chat.Api.Features.GetConversation;

public record Request(
    [property: BindFrom("RecipientId")]
    string RecipientId);

public record Response(
    [property: JsonPropertyName("id")]
    string ConversationId);