using Identity.API.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiRegisterTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task RegisterWithValidDataShouldReturns201WithUserId()
    {
        // Arrange
        var email = $"api-newuser-{Guid.NewGuid():N}@example.com";

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.Email.ShouldBe(email.ToLowerInvariant());
        body.UserId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterSendsVerificationEmailForNewUser()
    {
        // Arrange
        var email = $"api-verify-{Guid.NewGuid():N}@example.com";
        _testEmail.Reset();

        // Act
        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });

        // Assert
        _testEmail.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RegisterWithDuplicateEmailShouldReturnConflictStatusCode()
    {
        // Arrange
        var email = $"api-dup-{Guid.NewGuid():N}@example.com";
        var payload = new { email, password = "Pass123$", confirmPassword = "Pass123$" };

        // Act
        await _client.PostAsJsonAsync("/api/account/register", payload);
        var response = await _client.PostAsJsonAsync("/api/account/register", payload);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RegisterWithWeakPasswordShouldReturnBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email = $"api-weak-{Guid.NewGuid():N}@example.com",
            password = "123",
            confirmPassword = "123"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterWithMismatchedPasswordsShouldReturnBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email = $"api-mismatch-{Guid.NewGuid():N}@example.com",
            password = "Pass123$",
            confirmPassword = "Different123$"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.ShouldContainKey("ConfirmPassword");
    }
}
