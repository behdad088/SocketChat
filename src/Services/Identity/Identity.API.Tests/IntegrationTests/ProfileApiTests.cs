using Identity.API.Models;
using Identity.API.Tests.OidcFlowTests;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class ProfileApiTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";
    private const string ProfileUrl = "/api/account/profile";

    private readonly HttpClient _client = specification._factory!.CreateClient();

    private async Task<string> CreateUserAsync()
    {
        var email = $"profile-{Guid.NewGuid():N}@test.com";
        using var scope = specification._factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Name = "Initial",
            LastName = "User"
        };
        (await userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return email;
    }

    private async Task<string> GetTokenAsync(string email)
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, email, Password, scopes: "openid profile IdentityServerApi");
        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        return result.AccessToken!;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static HttpRequestMessage PutProfileRequest(
        string token, object body, string? ifMatch = null)
    {
        var request = AuthedRequest(HttpMethod.Put, ProfileUrl, token);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            System.Text.Encoding.UTF8,
            "application/json");
        if (ifMatch is not null)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue(ifMatch));
        }
        return request;
    }

    [Fact]
    public async Task GetProfileReturnsProfileWithEtag()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // Act
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, ProfileUrl, token));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag!.Tag.ShouldBe("\"0\"");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("email").GetString().ShouldBe(email);
        json.GetProperty("name").GetString().ShouldBe("Initial");
        json.GetProperty("lastName").GetString().ShouldBe("User");
    }

    [Fact]
    public async Task GetProfileWithoutTokenReturnsUnauthorized()
    {
        var response = await _client.GetAsync(ProfileUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutProfileWithoutIfMatchReturnsPreconditionRequired()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);
        
        // Act
        var response = await _client.SendAsync(PutProfileRequest(
            token, new { name = "New", lastName = "Name", profilePicture = "" }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task PutProfileWithStaleIfMatchReturnsPreconditionFailedAndChangesNothing()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // Act
        var first = await _client.SendAsync(PutProfileRequest(
            token, new { name = "First", lastName = "Update", profilePicture = "" }, "\"0\""));

        var second = await _client.SendAsync(PutProfileRequest(
            token, new { name = "Second", lastName = "Update", profilePicture = "" }, "\"0\""));

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
        
        var check = await _client.SendAsync(AuthedRequest(HttpMethod.Get, ProfileUrl, token));
        var json = JsonDocument.Parse(await check.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("name").GetString().ShouldBe("First");
    }

    [Fact]
    public async Task PutProfileWithCurrentIfMatchUpdatesAndIncrementsEtag()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // Act
        var response = await _client.SendAsync(PutProfileRequest(
            token,
            new { name = "Updated", lastName = "Person", profilePicture = "https://example.com/p.png" },
            "\"0\""));
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldBe("\"1\"");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("name").GetString().ShouldBe("Updated");
        json.GetProperty("lastName").GetString().ShouldBe("Person");
        json.GetProperty("profilePicture").GetString().ShouldBe("https://example.com/p.png");
    }

    [Fact]
    public async Task PutProfileWithOverlongFieldReturnsBadRequest()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // Act
        var response = await _client.SendAsync(PutProfileRequest(
            token,
            new { name = new string('x', 51), lastName = "Ok", profilePicture = "" },
            "\"0\""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PutProfileWithoutTokenReturnsUnauthorized()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Put, ProfileUrl)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        
        // Assert
        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
