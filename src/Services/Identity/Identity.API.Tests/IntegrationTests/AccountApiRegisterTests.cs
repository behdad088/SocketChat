using System.Net;
using System.Net.Http.Json;
using Identity.API.Endpoints;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AccountApiRegisterTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task Register_with_valid_data_returns_201_with_user_id()
    {
        var email = $"api-newuser-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        body!.Email.ShouldBe(email.ToLowerInvariant());
        body.UserId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_sends_verification_email_for_new_user()
    {
        var email = $"api-verify-{Guid.NewGuid():N}@example.com";
        _emailSpy.Reset();

        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });

        _emailSpy.GetLastSentCodeFor(email).ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        var email = $"api-dup-{Guid.NewGuid():N}@example.com";
        var payload = new { email, password = "Pass123$", confirmPassword = "Pass123$" };

        await _client.PostAsJsonAsync("/api/account/register", payload);
        var response = await _client.PostAsJsonAsync("/api/account/register", payload);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_with_weak_password_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email = $"api-weak-{Guid.NewGuid():N}@example.com",
            password = "123",
            confirmPassword = "123"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_with_mismatched_passwords_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/account/register", new
        {
            email = $"api-mismatch-{Guid.NewGuid():N}@example.com",
            password = "Pass123$",
            confirmPassword = "Different123$"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors.ShouldContainKey("ConfirmPassword");
    }
}
