namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiForgotPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task ForgotPasswordForExistingUserSendsResetEmailAndShouldReturn200()
    {
        // Arrange
        _testEmail.Reset();

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "AliceSmith@email.com" });
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _testEmail.GetLastSentCodeFor("AliceSmith@email.com").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPasswordForNonexistentEmailShouldReturn200()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "nobody-ever-registered@example.com" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPasswordWithInvalidEmailFormatShouldReturn400()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/forgot-password", new { email = "not-an-email" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
