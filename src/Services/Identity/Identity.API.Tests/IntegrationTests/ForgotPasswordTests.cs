namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class ForgotPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task ForgotPassword_get_page_returns_200()
    {
        var response = await _client.GetAsync("/Account/ForgotPassword");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_for_existing_user_sends_reset_email()
    {
        // Alice is seeded by UsersSeed
        _emailSpy.Reset();

        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = "AliceSmith@email.com"
            });

        response.IsSuccessStatusCode.ShouldBeTrue();
        _emailSpy.GetLastSentCodeFor("AliceSmith@email.com").ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ForgotPassword_for_nonexistent_email_does_not_leak_existence()
    {
        // The page should show the same UI regardless of whether the user exists
        var existingResponse = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = "AliceSmith@email.com"
            });

        var nonExistingResponse = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = "nobody-ever-registered@example.com"
            });

        var existingHtml = await existingResponse.Content.ReadAsStringAsync();
        var nonExistingHtml = await nonExistingResponse.Content.ReadAsStringAsync();

        // Both should render the same success-like message (anti-enumeration)
        existingHtml.ShouldNotContain("User not found");
        nonExistingHtml.ShouldNotContain("User not found");
    }

    [Fact]
    public async Task ForgotPassword_with_empty_email_returns_validation_error()
    {
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Input.Email"] = ""
            });

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("field-validation-error");
    }
}
