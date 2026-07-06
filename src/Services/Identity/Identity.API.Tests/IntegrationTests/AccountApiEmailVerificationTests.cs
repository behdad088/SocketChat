using System.Net;
using System.Net.Http.Json;
using Identity.API.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AccountApiEmailVerificationTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task Verify_with_valid_code_returns_200()
    {
        var email = $"api-ev-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _emailSpy.GetLastSentCodeFor(email);

        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Verify_with_nonexistent_code_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/account/email-verification", new { code = "DOESNOTEXIST99" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Verify_with_already_used_code_returns_410()
    {
        var email = $"api-ev-dup-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _emailSpy.GetLastSentCodeFor(email);

        await _client.PostAsJsonAsync("/api/account/email-verification", new { code });
        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Verify_with_expired_code_returns_410()
    {
        var email = $"api-ev-exp-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _emailSpy.GetLastSentCodeFor(email);

        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code!);
            var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task RegisterUserAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });
    }
}
