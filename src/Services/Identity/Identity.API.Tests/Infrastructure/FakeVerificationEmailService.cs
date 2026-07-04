using System.Collections.Concurrent;
using Identity.API.ApiClients.Mailtrap;
using Identity.API.Data;
using Identity.API.Services.EmailService;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.Tests.Infrastructure;

/// <summary>
/// Test double that stores verification codes in the DB (like the real service)
/// but skips the external Mailtrap HTTP call and exposes the plaintext codes for assertions.
/// </summary>
public class FakeVerificationEmailService(IServiceScopeFactory scopeFactory) : IVerificationEmailService
{
    private readonly ConcurrentDictionary<string, string> _codesByEmail =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> SentCodesByEmail => _codesByEmail;

    public string? GetLastSentCodeFor(string email) =>
        _codesByEmail.TryGetValue(email, out var code) ? code : null;

    public void Reset() => _codesByEmail.Clear();

    public async Task<bool> SendEmailAsync(
        string userEmail,
        string userId,
        EmailType emailType,
        string? returnUrl = null)
    {
        var plainCode = GenerateCode();
        _codesByEmail[userEmail] = plainCode;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var type = emailType == EmailType.EmailVerification
            ? EmailServiceConstants.EmailVerificationType
            : EmailServiceConstants.ForgotPasswordType;

        db.VerificationCodes.Add(new VerificationCode
        {
            UserId = userId,
            Code = VerificationCodeHasher.Hash(plainCode),
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return true;
    }

    private static string GenerateCode() =>
        Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
}
