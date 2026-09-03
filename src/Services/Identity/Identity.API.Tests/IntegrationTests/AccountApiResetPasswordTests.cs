using Identity.API.Endpoints;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiResetPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task ValidateWithValidCodeShouldReturnOkWithUserId()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        // Act
        var response = await _client.GetAsync($"/api/account/reset-password/validate?code={code}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResetCodeResponse>();
        body!.UserId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateWithNonexistentCodeShouldReturnBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/account/reset-password/validate?code=DOESNOTEXIST9");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateWithExpiredCodeShouldReturnsGoneStatusCode()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        await BackdateCodeAsync(code, TimeSpan.FromMinutes(31));

        // Act
        var response = await _client.GetAsync($"/api/account/reset-password/validate?code={code}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task<string> IssueForgotPasswordCodeForAliceAsync()
    {
        _testEmail.Reset();
        await _client.PostAsJsonAsync("/api/account/forgot-password", new { email = "AliceSmith@email.com" });
        var code = _testEmail.GetLastSentCodeFor("AliceSmith@email.com");
        code.ShouldNotBeNullOrEmpty("Expected a reset code to be sent to Alice");
        return code;
    }

    private async Task BackdateCodeAsync(string plainCode, TimeSpan by)
    {
        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(plainCode);
        var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
        entry.CreatedAt = DateTime.UtcNow.Subtract(by);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ResetWithValidCodeAndMatchingUserShouldReturnOk()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass999!",
            confirmPassword = "NewPass999!"
        });
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetMarksTheCodeActivatedAfterUse()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        // Act
        await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass111!",
            confirmPassword = "NewPass111!"
        });

        // Assert
        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
        entry.IsActivated.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetWithMismatchedPasswordShouldReturnBadRequest()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass999!",
            confirmPassword = "Different999!"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetWithExpiredCodeSubmittedDirectlyIsRejected()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        await BackdateCodeAsync(code, TimeSpan.FromMinutes(31));

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var aliceUserId = (await db.VerificationCodes.SingleAsync(c => c.Code == hashed)).UserId;

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = aliceUserId,
            code,
            password = "NewPass222!",
            confirmPassword = "NewPass222!"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task ResetWithValidCodeButSomeoneElsesUserIdIsRejected()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bobUserId = (await db.Users.SingleAsync(u => u.Email == "BobSmith@email.com")).Id;

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = bobUserId,
            code,
            password = "NewPass333!",
            confirmPassword = "NewPass333!"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
