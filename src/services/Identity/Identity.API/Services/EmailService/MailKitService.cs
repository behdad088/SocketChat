using Identity.API.ApiClients.Mailtrap;
using Identity.API.Data;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Identity.API.Services.EmailService;

public class MailKitService(
    ApplicationDbContext dbContext,
    IOptions<MailTrapServicesSettings> mailTrapSettings,
    IConfiguration config) : IVerificationEmailService
{
    private string? _returnUrl = string.Empty;
    private string? _emailType = string.Empty;

    public async Task<bool> SendEmailAsync(
        string userEmail,
        string userId,
        EmailType emailType,
        string? returnUrl = null)
    {
        _emailType = emailType switch
        {
            EmailType.EmailVerification => EmailServiceConstants.EmailVerificationType,
            EmailType.ForgotPassword => EmailServiceConstants.ForgotPasswordType,
            _ => throw new ArgumentOutOfRangeException(nameof(emailType), emailType, null)
        };
        _returnUrl = returnUrl ?? string.Empty;

        var verificationCode = await SaveVerificationCodeAsync(userId);
        var verificationLink = EmailVerificationLink(verificationCode);

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Identity Service", mailTrapSettings.Value.Sender!));
        message.To.Add(new MailboxAddress("Identity Service", userEmail));
        message.Subject = emailType switch
        {
            EmailType.EmailVerification => "Verify your email",
            EmailType.ForgotPassword => "Reset your password",
            _ => throw new ArgumentOutOfRangeException(nameof(emailType), emailType, null)
        };
        message.Body = new TextPart("plain")
        {
            Text = emailType switch
            {
                EmailType.EmailVerification =>
                    $"Please click the link below to verify your email address. {verificationLink}",
                EmailType.ForgotPassword =>
                    $"Please click the link below to reset your password. {verificationLink}",
                _ => throw new ArgumentOutOfRangeException(nameof(emailType), emailType, null)
            }
        };
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(mailTrapSettings.Value.BaseUrl!, 1025);
        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);
        return true;
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

    private string GetPage()
    {
        return _emailType! switch
        {
            EmailServiceConstants.EmailVerificationType => "email-verification",
            EmailServiceConstants.ForgotPasswordType => "reset-password",
            _ => throw new ArgumentOutOfRangeException(nameof(_emailType), _emailType, null)
        };
    }

    private static string GenerateVerificationCode()
    {
        var code = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return code;
    }
}
