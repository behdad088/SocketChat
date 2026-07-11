using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Identity.API.Models;
using Identity.API.Tests.OidcFlowTests;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

// Rate-limit budget: the shared "profile" rate limiter allows 10 requests/min per factory
// (QueueLimit 0, and even 401s consume permits since UseRateLimiter runs before UseAuthorization).
// This class currently issues 9 requests against /api/account/profile — keep the total at or
// below 10 or these tests will intermittently 429.
[Collection(IntegrationTestCollection.Name)]
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
    public async Task Get_profile_returns_profile_with_etag()
    {
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, ProfileUrl, token));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag!.Tag.ShouldBe("\"0\"");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("email").GetString().ShouldBe(email);
        json.GetProperty("name").GetString().ShouldBe("Initial");
        json.GetProperty("lastName").GetString().ShouldBe("User");
    }

    [Fact]
    public async Task Get_profile_without_token_returns_401()
    {
        var response = await _client.GetAsync(ProfileUrl);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Put_profile_without_if_match_returns_428()
    {
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        var response = await _client.SendAsync(PutProfileRequest(
            token, new { name = "New", lastName = "Name", profilePicture = "" }));

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionRequired);
    }

    [Fact]
    public async Task Put_profile_with_stale_if_match_returns_412_and_changes_nothing()
    {
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // First update with version 0 succeeds and bumps the version.
        var first = await _client.SendAsync(PutProfileRequest(
            token, new { name = "First", lastName = "Update", profilePicture = "" }, "\"0\""));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Second update with the old ETag (0) is stale since version is now 1.
        var second = await _client.SendAsync(PutProfileRequest(
            token, new { name = "Second", lastName = "Update", profilePicture = "" }, "\"0\""));
        second.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);

        var check = await _client.SendAsync(AuthedRequest(HttpMethod.Get, ProfileUrl, token));
        var json = JsonDocument.Parse(await check.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("name").GetString().ShouldBe("First");
    }

    [Fact]
    public async Task Put_profile_with_current_if_match_updates_and_increments_etag()
    {
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        // A fresh user starts at Version 0, so the current ETag is "0".
        var response = await _client.SendAsync(PutProfileRequest(
            token,
            new { name = "Updated", lastName = "Person", profilePicture = "https://example.com/p.png" },
            "\"0\""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag!.Tag.ShouldBe("\"1\"");

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("name").GetString().ShouldBe("Updated");
        json.GetProperty("lastName").GetString().ShouldBe("Person");
        json.GetProperty("profilePicture").GetString().ShouldBe("https://example.com/p.png");
    }

    [Fact]
    public async Task Put_profile_with_overlong_field_returns_400()
    {
        var email = await CreateUserAsync();
        var token = await GetTokenAsync(email);

        var response = await _client.SendAsync(PutProfileRequest(
            token,
            new { name = new string('x', 51), lastName = "Ok", profilePicture = "" },
            "\"0\""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_profile_without_token_returns_401()
    {
        var request = new HttpRequestMessage(HttpMethod.Put, ProfileUrl)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
        };
        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
