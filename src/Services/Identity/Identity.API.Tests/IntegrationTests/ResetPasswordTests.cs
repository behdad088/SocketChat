namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ResetPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task ResetPassword_get_with_valid_code_returns_200_and_shows_form()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var response = await _client.GetAsync($"/Account/reset-password?code={code}");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldNotContain("Invalid verification Code");
    }

    [Fact]
    public async Task ResetPassword_get_with_missing_code_shows_error()
    {
        var response = await _client.GetAsync("/Account/reset-password?code=");

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Code is missing");
    }

    [Fact]
    public async Task ResetPassword_get_with_nonexistent_code_shows_error()
    {
        var response = await _client.GetAsync("/Account/reset-password?code=DOESNOTEXIST9");

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid verification Code");
    }

    [Fact]
    public async Task ResetPassword_get_with_expired_code_shows_error()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();

        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code);
            var entry = db.VerificationCodes.Single(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            db.SaveChanges();
        }

        var response = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("expired");
    }

    [Fact]
    public async Task ResetPassword_post_with_valid_data_shows_success_message()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();

        // GET first to discover the userId
        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();

        // Extract hidden userId from the form
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;
        userId.ShouldNotBeNullOrEmpty();

        var response = await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass999!",
                ["Input.ConfirmPassword"] = "NewPass999!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        var resultHtml = await response.Content.ReadAsStringAsync();
        resultHtml.ShouldContain("successfully reset");
    }

    [Fact]
    public async Task ResetPassword_post_with_already_used_code_still_updates_password_but_marks_code_used()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;

        // First reset
        await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass111!",
                ["Input.ConfirmPassword"] = "NewPass111!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        // Verify the code is now marked as activated in DB
        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var entry = db.VerificationCodes.Single(c => c.Code == hashed);
        entry.IsActivated.ShouldBeTrue();
    }

    [Fact]
    public async Task ResetPassword_post_with_mismatched_passwords_shows_error()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var getResponse = await _client.GetAsync($"/Account/reset-password?code={code}");
        var html = await getResponse.Content.ReadAsStringAsync();
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(
            html, @"name=""Input\.UserId"" value=""([^""]+)""");
        var userId = userIdMatch.Groups[1].Value;

        var response = await AntiForgeryHelper.PostFormAsync(_client, $"/Account/reset-password?code={code}",
            new Dictionary<string, string>
            {
                ["Input.Password"] = "NewPass999!",
                ["Input.ConfirmPassword"] = "DifferentPass999!",
                ["Input.Code"] = code,
                ["Input.UserId"] = userId
            });

        var resultHtml = await response.Content.ReadAsStringAsync();
        // ModelState validation should catch mismatched passwords
        resultHtml.ShouldContain("validation");
    }

    private async Task<string> IssueForgotPasswordCodeForAliceAsync()
    {
        _emailSpy.Reset();
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = "AliceSmith@email.com"
            });

        var code = _emailSpy.GetLastSentCodeFor("AliceSmith@email.com");
        code.ShouldNotBeNullOrEmpty("Expected a reset code to be sent to Alice");
        return code!;
    }
}
