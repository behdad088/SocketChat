namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class ResetPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task ResetPasswordGetWithValidCodeReturnsOkAndShowsForm()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();
        
        // Act
        var response = await _client.GetAsync($"/Account/reset-password?code={code}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldNotContain("Invalid verification Code");
    }

    [Fact]
    public async Task ResetPasswordGetWithMissingCodeShowsError()
    {
        // Act
        var response = await _client.GetAsync("/Account/reset-password?code=");
        
        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Code is missing");
    }

    [Fact]
    public async Task ResetPasswordGetWithNonexistentCodeShowsError()
    {
        // Arrange
        var code = "DOESNOTEXIST9";
        // Act
        var response = await _client.GetAsync($"/Account/reset-password?code={code}");

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid verification Code");
    }

    [Fact]
    public async Task ResetPasswordGetWithExpiredCodeShowsError()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code);
            var entry = db.VerificationCodes.Single(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.GetAsync($"/Account/reset-password?code={code}");
        
        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("expired");
    }

    [Fact]
    public async Task ResetPasswordPostWithValidDataShowsSuccessMessage()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();

        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;
        userId.ShouldNotBeNullOrEmpty();

        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass999!",
                ["Input.ConfirmPassword"] = "NewPass999!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        // Assert
        var resultHtml = await response.Content.ReadAsStringAsync();
        resultHtml.ShouldContain("successfully reset");
    }

    [Fact]
    public async Task ResetPasswordPostWithAlreadyUsedCodeStillUpdatesPasswordButMarkscodeUsed()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;

        // Act
        await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass111!",
                ["Input.ConfirmPassword"] = "NewPass111!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        // Assert
        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var entry = db.VerificationCodes.Single(c => c.Code == hashed);
        entry.IsActivated.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPasswordPostWithMismatchedPasswordsShowsError()
    {
        // Arrange
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;

        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass999!",
                ["Input.ConfirmPassword"] = "DifferentPass999!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        // Assert
        var resultHtml = await response.Content.ReadAsStringAsync();
        resultHtml.ShouldContain("validation");
    }

    private async Task<string> IssueForgotPasswordCodeForAliceAsync()
    {
        _testEmail.Reset();
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = "AliceSmith@email.com"
            });

        var code = _testEmail.GetLastSentCodeFor("AliceSmith@email.com");
        code.ShouldNotBeNullOrEmpty("Expected a reset code to be sent to Alice");
        return code;
    }
}
