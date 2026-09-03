namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiResendVerificationEmailTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task ResendForExistingUnverifiedUserSendsNewEmailAndShouldReturnsOk()
    {
        // Arrange
        var email = $"api-resend-{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });
        _testEmail.Reset();
        
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email", new { email });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _testEmail.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResendForNonexistentEmailStillShouldReturnOk()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email",
            new { email = "nobody-ever-registered@example.com" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResendWithInvalidEmailFormatShouldReturnBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/register/resend-verification-email",
            new { email = "not-an-email" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
