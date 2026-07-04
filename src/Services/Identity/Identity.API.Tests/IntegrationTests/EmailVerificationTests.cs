namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class EmailVerificationTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task VerifyCode_get_page_returns_200()
    {
        var response = await _client.GetAsync("/Account/email-verification?code=DUMMY");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyCode_with_valid_code_returns_success()
    {
        var email = $"ev-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _emailSpy.GetLastSentCodeFor(email);
        code.ShouldNotBeNullOrEmpty();

        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        response.IsSuccessStatusCode.ShouldBeTrue();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("successfully verified");
    }

    [Fact]
    public async Task VerifyCode_with_missing_code_returns_failure()
    {
        var response = await _client.GetAsync(
            "/Account/email-verification?handler=VerifyCode&code=");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"success\":false");
    }

    [Fact]
    public async Task VerifyCode_with_nonexistent_code_returns_failure()
    {
        var response = await _client.GetAsync(
            "/Account/email-verification?handler=VerifyCode&code=DOESNOTEXIST99");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"success\":false");
    }

    [Fact]
    public async Task VerifyCode_with_already_used_code_returns_failure()
    {
        var email = $"ev-dup-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _emailSpy.GetLastSentCodeFor(email);

        // First verification succeeds
        await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        // Second use of the same code must fail
        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("already been activated");
    }

    [Fact]
    public async Task VerifyCode_with_expired_code_returns_failure()
    {
        var email = $"ev-exp-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);

        var code = _emailSpy.GetLastSentCodeFor(email);

        // Manually backdate the verification code in the DB
        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code!);
            var entry = db.VerificationCodes.Single(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            db.SaveChanges();
        }

        var response = await _client.GetAsync(
            $"/Account/email-verification?handler=VerifyCode&code={code}");

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
