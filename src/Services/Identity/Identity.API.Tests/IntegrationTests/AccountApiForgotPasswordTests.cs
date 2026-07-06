using System.Net;
using System.Net.Http.Json;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AccountApiForgotPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task ForgotPassword_for_existing_user_sends_reset_email_and_returns_200()
    {
        _emailSpy.Reset();

        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "AliceSmith@email.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _emailSpy.GetLastSentCodeFor("AliceSmith@email.com").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPassword_for_nonexistent_email_returns_same_200()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "nobody-ever-registered@example.com" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_with_invalid_email_format_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "not-an-email" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
