using Refit;

namespace Identity.API.ApiClients.Mailtrap;

public interface IMailTrapServicesClient
{
    [Post("/api/send")]
    Task<SendVerificationEmailResponse> SendEmailAsync(
        [Header("Authorization")] string authorizationKey,
        [Body] SendVerificationEmailRequest request,
        CancellationToken cancellationToken
    );
}