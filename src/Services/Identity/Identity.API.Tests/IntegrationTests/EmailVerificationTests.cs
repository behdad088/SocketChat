namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class EmailVerificationTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task VerifyCodeGetPageReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/Account/email-verification?code=DUMMY");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyCodeWithValidCodeReturnsSuccess()
    {
        // Arrange
        var email = $"ev-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _testEmail.GetLastSentCodeFor(email);
        code.ShouldNotBeNullOrEmpty();

        // Act
        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("successfully verified");
    }

    [Fact]
    public async Task VerifyCodeWithMissingCodeReturnsFailure()
    {
        // Act
        var response = await _client.GetAsync(
            "/Account/email-verification?handler=VerifyCode&code=");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"success\":false");
    }

    [Fact]
    public async Task VerifyCodeWithNonexistentCodeReturnsFailure()
    {
        // Act
        var response = await _client.GetAsync(
            "/Account/email-verification?handler=VerifyCode&code=DOESNOTEXIST99");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"success\":false");
    }

    [Fact]
    public async Task VerifyCodeWithAlreadyUsedCodeReturnsFailure()
    {
        // Arrange
        var email = $"ev-dup-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _testEmail.GetLastSentCodeFor(email);

        // Act
        await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        
        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("already been activated");
    }

    [Fact]
    public async Task VerifyCodeWithExpiredCodeReturnsFailure()
    {
        // Arrange
        var email = $"ev-exp-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _testEmail.GetLastSentCodeFor(email);

        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code!);
            var entry = db.VerificationCodes.Single(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }
        
        // Act
        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("expired");
    }

    private async Task RegisterUserAsync(string email)
    {
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });
    }
}
