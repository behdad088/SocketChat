using Identity.API.Models;
using Identity.API.Services.Account;
using Identity.API.Tests.OidcFlowTests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiDeleteAccountTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";
    private const string AccountUrl = "/api/account";

    private readonly HttpClient _client = specification.CreateClientAndBindSpy();

    [Fact]
    public async Task DeleteAccountReturnsNoContentAndRemovesTheUser()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);

        // Act
        var response = await _client.SendAsync(DeleteRequest(token));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindUserAsync(user.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAccountOnlyDeletesTheCallersAccount()
    {
        // Arrange
        var caller = await CreateUserAsync();
        var otherUser = await CreateUserAsync();
        var token = await GetTokenAsync(caller.Email!);

        // Act
        var response = await _client.SendAsync(DeleteRequest(token));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindUserAsync(otherUser.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAccountWithWrongPasswordReturnsForbiddenAndKeepsTheUser()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);

        // Act
        var response = await _client.SendAsync(DeleteRequest(token, password: "WrongPass1!"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await FindUserAsync(user.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAccountWithEmptyPasswordReturnsBadRequestAndKeepsTheUser()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);

        // Act
        var response = await _client.SendAsync(DeleteRequest(token, password: string.Empty));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FindUserAsync(user.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task RepeatedWrongPasswordsLockTheAccount()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);

        // Act
        for (var i = 0; i < 5; i++)
        {
            var response = await _client.SendAsync(DeleteRequest(token, password: "WrongPass1!"));
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // Assert
        using var scope = specification._factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await userManager.FindByIdAsync(user.Id);
        stored.ShouldNotBeNull();
        (await userManager.IsLockedOutAsync(stored)).ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteAccountForAnAccountWithoutAPasswordIsRefused()
    {
        // Arrange
        var email = $"external-{Guid.NewGuid():N}@test.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        using var scope = specification._factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();

        // Act
        var result = await accountService.DeleteAccountAsync(user.Id, "AnyPass1!");

        // Assert
        result.Succeeded.ShouldBeFalse();
        result.ErrorCode.ShouldBe(AccountErrorCode.PasswordRequired);
        (await FindUserAsync(user.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAccountWithoutTokenReturnsUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync(AccountUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletedUsersTokenIsRejected()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);
        (await _client.SendAsync(DeleteRequest(token))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        var response = await _client.SendAsync(DeleteRequest(token));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnotherClientsAnonymousRequestsDoNotUseUpTheDeleteLimit()
    {
        // Arrange
        var user = await CreateUserAsync();
        var token = await GetTokenAsync(user.Email!);
        for (var i = 0; i < 5; i++)
        {
            var anonymous = await SendAnonymousDeleteFromAsync("10.0.0.1");
            anonymous.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        }

        // Act
        var response = await _client.SendAsync(DeleteRequest(token));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private Task<HttpContext> SendAnonymousDeleteFromAsync(string ipAddress) =>
        specification._factory!.Server.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Delete;
            context.Request.Scheme = "http";
            context.Request.Host = new HostString("localhost");
            context.Request.Path = AccountUrl;
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        });

    private static HttpRequestMessage DeleteRequest(string token, string password = Password)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, AccountUrl)
        {
            Content = JsonContent.Create(new { password })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<ApplicationUser> CreateUserAsync()
    {
        var email = $"delete-{Guid.NewGuid():N}@test.com";
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
        return user;
    }

    private async Task<ApplicationUser?> FindUserAsync(string userId)
    {
        using var scope = specification._factory!.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await userManager.FindByIdAsync(userId);
    }

    private async Task<string> GetTokenAsync(string email)
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, email, Password, scopes: "openid profile IdentityServerApi");
        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        return result.AccessToken!;
    }
}
