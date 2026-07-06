using System.Net;
using System.Net.Http.Json;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AccountApiResendVerificationEmailTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task Resend_for_existing_unverified_user_sends_new_email_and_returns_200()
    {
        var email = $"api-resend-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });
        _emailSpy.Reset();

        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email", new { email });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _emailSpy.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Resend_for_nonexistent_email_still_returns_200()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email",
            new { email = "nobody-ever-registered@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resend_with_invalid_email_format_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email",
            new { email = "not-an-email" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
