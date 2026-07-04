namespace Identity.API.Services.EmailService;

public interface IVerificationEmailService
{
    Task<bool> SendEmailAsync(string userEmail, string userId, EmailType emailType, string? returnUrl = null);
}