using System.Net;
using System.Net.Http.Json;
using Identity.API.Data;
using Identity.API.Endpoints;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class AccountApiResetPasswordTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly FakeVerificationEmailService _emailSpy = specification.EmailSpy;

    [Fact]
    public async Task Validate_with_valid_code_returns_200_with_user_id()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();

        var response = await _client.GetAsync($"/api/account/reset-password/validate?code={code}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ValidateResetCodeResponse>();
        body!.UserId.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Validate_with_nonexistent_code_returns_400()
    {
        var response = await _client.GetAsync("/api/account/reset-password/validate?code=DOESNOTEXIST9");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Validate_with_expired_code_returns_410()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();
        await BackdateCodeAsync(code, TimeSpan.FromMinutes(31));

        var response = await _client.GetAsync($"/api/account/reset-password/validate?code={code}");

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task<string> IssueForgotPasswordCodeForAliceAsync()
    {
        _emailSpy.Reset();
        await _client.PostAsJsonAsync("/api/account/forgot-password", new { email = "AliceSmith@email.com" });
        var code = _emailSpy.GetLastSentCodeFor("AliceSmith@email.com");
        code.ShouldNotBeNullOrEmpty("Expected a reset code to be sent to Alice");
        return code!;
    }

    private async Task BackdateCodeAsync(string plainCode, TimeSpan by)
    {
        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(plainCode);
        var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
        entry.CreatedAt = DateTime.UtcNow.Subtract(by);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reset_with_valid_code_and_matching_user_returns_200()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass999!",
            confirmPassword = "NewPass999!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_marks_the_code_activated_after_use()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass111!",
            confirmPassword = "NewPass111!"
        });

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
        entry.IsActivated.ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_with_mismatched_passwords_returns_400()
    {
        var code = await IssueForgotPasswordCodeForAliceAsync();
        var validate = await _client.GetFromJsonAsync<ValidateResetCodeResponse>(
            $"/api/account/reset-password/validate?code={code}");

        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = validate!.UserId,
            code,
            password = "NewPass999!",
            confirmPassword = "Different999!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reset_with_expired_code_submitted_directly_is_rejected()
    {
        // Regression test for the fix: POST must re-validate the code, not just
        // trust the submitted userId (previously only OnGet validated it).
        var code = await IssueForgotPasswordCodeForAliceAsync();
        await BackdateCodeAsync(code, TimeSpan.FromMinutes(31));

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var aliceUserId = (await db.VerificationCodes.SingleAsync(c => c.Code == hashed)).UserId;

        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = aliceUserId,
            code,
            password = "NewPass222!",
            confirmPassword = "NewPass222!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task Reset_with_valid_code_but_someone_elses_user_id_is_rejected()
    {
        // Regression test for the fix: a valid code for Alice must not be usable
        // to reset a different user's (Bob's) password by swapping the userId.
        var code = await IssueForgotPasswordCodeForAliceAsync();

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bobUserId = (await db.Users.SingleAsync(u => u.Email == "BobSmith@email.com")).Id;

        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = bobUserId,
            code,
            password = "NewPass333!",
            confirmPassword = "NewPass333!"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
