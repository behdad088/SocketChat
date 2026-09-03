namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
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
    public async Task LoginGetPageReturnsOk()
    {
        // Act
        var response = await _followRedirectClient.GetAsync("/Account/Login");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginWithValidAliceCredentialsRedirects()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "AliceSmith@email.com",
                ["Input.Password"] = "Pass123$",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldNotContain("Invalid username or password");
    }

    [Fact]
    public async Task LoginWithWrongPasswordShowsError()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "AliceSmith@email.com",
                ["Input.Password"] = "WrongPassword!",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });
        
        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid username or password");
    }

    [Fact]
    public async Task LoginWithNonexistentUserShowsError()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "nobody@example.com",
                ["Input.Password"] = "Pass123$",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        // Assert
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Invalid username or password");
    }

    [Fact]
    public async Task LoginWithEmptyCredentialsReturnsValidationErrors()
    {
        // Act
        var response = await AntiForgeryHelper.PostFormAsync(_followRedirectClient, "/Account/Login",
            new Dictionary<string, string>
            {
                ["Input.Username"] = "",
                ["Input.Password"] = "",
                ["Input.RememberLogin"] = "false",
                ["Input.Button"] = "login"
            });

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("validation-summary-errors");
    }
}
