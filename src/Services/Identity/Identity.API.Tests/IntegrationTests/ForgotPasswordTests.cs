namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class ForgotPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task ForgotPasswordGetPageReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/Account/ForgotPassword");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPasswordForExistingUserSendsResetEmail()
    {
        // Arrange
        var email = "AliceSmith@email.com";
        _testEmail.Reset();
        
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email
            });

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        _testEmail.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPasswordForNonexistentEmailDoesNotLeakExistence()
    {
        // Arrange
        const string aliceEmail = "AliceSmith@email.com";
        const string nonexistentEmail = "nobody-ever-registered@example.com";
        
        // Act
        var existingResponse = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = aliceEmail
            });

        var nonExistingResponse = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = nonexistentEmail
            });

        // Assert
        var existingHtml = await existingResponse.Content.ReadAsStringAsync();
        var nonExistingHtml = await nonExistingResponse.Content.ReadAsStringAsync();

        existingHtml.ShouldNotContain("User not found");
        nonExistingHtml.ShouldNotContain("User not found");
    }

    [Fact]
    public async Task ForgotPasswordWithEmptyEmailReturnsValidationRrror()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = ""
            });

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("field-validation-error");
    }
}
