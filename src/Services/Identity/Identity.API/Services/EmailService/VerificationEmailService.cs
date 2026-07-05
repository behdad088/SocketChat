using Identity.API.ApiClients.Mailtrap;
using Identity.API.Data;
using Microsoft.Extensions.Options;

namespace Identity.API.Services.EmailService;

public class VerificationEmailService(
    ApplicationDbContext dbContext,
    IMailTrapServicesClient mailTrapServicesClient,
    IOptions<MailTrapServicesSettings> mailTrapSettings,
    IConfiguration config) : IVerificationEmailService
{
    private string? _returnUrl = string.Empty;
    private string? _emailType = string.Empty;
    public async Task<bool> SendEmailAsync(string userEmail, string userId, EmailType emailType, string? returnUrl = null)
    {
        _emailType = emailType switch
        {
            EmailType.EmailVerification => EmailServiceConstants.EmailVerificationType,
            EmailType.ForgotPassword => EmailServiceConstants.ForgotPasswordType,
            _ => throw new ArgumentOutOfRangeException(nameof(emailType), emailType, null)
        };

        _returnUrl = returnUrl ?? string.Empty;
        var verificationCode = await SaveVerificationCodeAsync(userId);
        var result = await SendVerificationEmailAsync(verificationCode, userEmail)
            .ConfigureAwait(false);
        return result;
    }
    
    
    
    private async Task<bool> SendVerificationEmailAsync(
        string verificationCode,
        string email)
    {
        var request = CreateSendVerificationEmailRequest(verificationCode, email);
        var result = await mailTrapServicesClient
            .SendEmailAsync(
                authorizationKey: mailTrapSettings.Value.AuthorizationKey!,
                request: request,
                cancellationToken: CancellationToken.None)
            .ConfigureAwait(false);
        
        if (!result.Success)
        {
            throw new Exception("Failed to send verification email.");
        }
        
        return result.Success;
    }
    
    private SendVerificationEmailRequest CreateSendVerificationEmailRequest(
        string verificationCode,
        string email)
    {
        var verificationLink = EmailVerificationLink(verificationCode);
        var templateId = GetTemplateId();
        return new SendVerificationEmailRequest(
            From: new From(mailTrapSettings.Value.Sender!),
            To: [new To(email)],
            TemplateUuid: templateId
            ,
            new TemplateVariables(
                CompanyInfoName: "Test Company",
                CustomerName: email.Split('@')[0],
                VerificationLink: verificationLink)
        );
    }
    
    private string EmailVerificationLink(string verificationCode)
    {
        var serviceUrl = config.GetValue<string>("service_url") ?? throw new Exception("ServiceUrl is not configured.");
        serviceUrl = $"{serviceUrl}/account/{GetPage()}?code={verificationCode}";

        if (!string.IsNullOrEmpty(_returnUrl))
        {
            serviceUrl += $"&returnUrl={Uri.EscapeDataString(_returnUrl)}";
        }
        
        return serviceUrl;
    }

    private string GetTemplateId()
    {
        return _emailType! switch
        {
            EmailServiceConstants.EmailVerificationType => EmailServiceConstants.VerifyEmailTemplateId,
            EmailServiceConstants.ForgotPasswordType => EmailServiceConstants.ForgotPasswordTemplateId,
            _ => throw new ArgumentOutOfRangeException(nameof(_emailType), _emailType, null)
        };
    }
    
    private string GetPage()
    {
        return _emailType! switch
        {
            EmailServiceConstants.EmailVerificationType => "email-verification",
            EmailServiceConstants.ForgotPasswordType => "reset-password",
            _ => throw new ArgumentOutOfRangeException(nameof(_emailType), _emailType, null)
        };
    }
    
    private async Task<string> SaveVerificationCodeAsync(string userId)
    {
        var plaintext = GenerateVerificationCode();
        var verificationCodeEntity = new VerificationCode
        {
            UserId = userId,
            Code = VerificationCodeHasher.Hash(plaintext),
            Type = _emailType!,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.VerificationCodes.Add(verificationCodeEntity);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);

        return plaintext;
    }
    
    private static string GenerateVerificationCode()
    {
        var code = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return code;
    }
}