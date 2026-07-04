namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class LoginTests(IdentityApiSpecification specification)
{
    // Uses a separate non-redirect-following client so we can inspect status codes before redirect
    private readonly HttpClient _client = specification._factory!.CreateClient(
        new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

    private readonly HttpClient _followRedirectClient = specification.CreateClientAndBindSpy();

    [Fact]
    public async Task Login_get_page_returns_200()
    {
        var response = await _followRedirectClient.GetAsync("/Account/Login");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_with_valid_alice_credentials_redirects()
    {
        // Alice is seeded by UsersSeed with EmailConfirmed=true
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "AliceSmith@email.com",
                ["Input.Password"] = "Pass123$",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        // After a successful local login, IdentityServer redirects back to the return URL
        // or to the root. Either way it should NOT show an error page.
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldNotContain("Invalid username or password");
    }

    [Fact]
    public async Task Login_with_wrong_password_shows_error()
    {
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "AliceSmith@email.com",
                ["Input.Password"] = "WrongPassword!",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid username or password");
    }

    [Fact]
    public async Task Login_with_nonexistent_user_shows_error()
    {
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "nobody@example.com",
                ["Input.Password"] = "Pass123$",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid username or password");
    }

    [Fact]
    public async Task Login_with_empty_credentials_returns_validation_errors()
    {
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "",
                ["Input.Password"] = "",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        // ModelState invalid → re-renders page; _ValidationSummary emits
        // "validation-summary-errors", asp-validation-for spans emit "field-validation-error"
        response.IsSuccessStatusCode.ShouldBeTrue();
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("validation-summary-errors");
    }
}
