namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class RegisterTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task Register_with_valid_data_redirects_and_returns_success()
    {
        var email = $"newuser-{Guid.NewGuid():N}@example.com";

        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        response.StatusCode.ShouldNotBe(System.Net.HttpStatusCode.BadRequest);
        // PRG pattern: final page should be 200 after redirect
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_sends_verification_email_for_new_user()
    {
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        _emailSpy.Reset();

        await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        _emailSpy.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_error_page()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        // First registration succeeds
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        // Second registration with same email should return the page with an error
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("already exists");
    }

    [Fact]
    public async Task Register_with_weak_password_returns_validation_error()
    {
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = $"weakpw-{Guid.NewGuid():N}@example.com",
                ["Input.Password"] = "123",
                ["Input.ConfirmPassword"] = "123"
            });

        var html = await response.Content.ReadAsStringAsync();
        // ModelState validation error or identity error should be present
        html.ShouldContain("validation-summary-errors", Case.Sensitive);
    }

    [Fact]
    public async Task Register_get_page_returns_200()
    {
        var response = await _client.GetAsync("/Account/Register");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }
}
