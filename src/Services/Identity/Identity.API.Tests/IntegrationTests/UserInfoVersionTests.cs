using Identity.API.Models;
using Identity.API.Tests.OidcFlowTests;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class UserInfoVersionTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";
    private const string UserInfoUrl = "/connect/userinfo";

    private readonly HttpClient _client = specification._factory!.CreateClient();

    private async Task<string> CreateUserAsync()
    {
        var email = $"userinfo-{Guid.NewGuid():N}@test.com";
        using var scope = specification._factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            Name = "UserInfo",
            LastName = "Tester"
        };
        (await userManager.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
        return email;
    }

    private async Task<HttpResponseMessage> GetUserInfoAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task UserinfoReturnsVersionAsEtagHeaderAndBodyClaim()
    {
        // Arrange
        var email = await CreateUserAsync();
        var token = await TokenHelper.RequestPasswordTokenAsync(
            _client, email, Password, scopes: "openid profile");
        token.IsError.ShouldBeFalse(token.Error ?? "Unexpected error");

        // Act
        var response = await GetUserInfoAsync(token.AccessToken!);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag!.Tag.ShouldBe("\"0\"");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("version").GetString().ShouldBe("0");
    }

    /// <summary>
    /// A token-carried version would go stale for the token's lifetime;
    /// the claim must only be issued live by the userinfo endpoint
    /// </summary>
    [Fact]
    public async Task VersionClaimIsNotBakedIntoAccessTokens()
    {
        var email = await CreateUserAsync();
        var token = await TokenHelper.RequestPasswordTokenAsync(
            _client, email, Password, scopes: "openid profile");
        token.IsError.ShouldBeFalse(token.Error ?? "Unexpected error");

        TokenHelper.ParseClaim(token.AccessToken!, "version").ShouldBeNull();
    }
}
