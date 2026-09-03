namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class RegisterTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task RegisterWithValidDataRedirectsAndReturnsSuccess()
    {
        // Arrange
        var email = $"newuser-{Guid.NewGuid():N}@example.com";

        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        // Assert
        response.StatusCode.ShouldNotBe(HttpStatusCode.BadRequest);
        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task RegisterSendsVerificationEmailForNewUser()
    {
        // Arrange
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        _testEmail.Reset();

        // Act
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        // Assert
        _testEmail.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterWithDuplicateEmailReturnsErrorPage()
    {
        // Arrange
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        // Act
        await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = "Pass123$",
                ["Input.ConfirmPassword"] = "Pass123$"
            });

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("already exists");
    }

    [Fact]
    public async Task RegisterWithWeakPasswordReturnsValidationError()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_client, "/Account/Register",
            new Dictionary<string, string>
            {
                ["Input.Email"] = $"weakpw-{Guid.NewGuid():N}@example.com",
                ["Input.Password"] = "123",
                ["Input.ConfirmPassword"] = "123"
            });

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("validation-summary-errors", Case.Sensitive);
    }

    [Fact]
    public async Task RegisterGetPageReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/Account/Register");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
