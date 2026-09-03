namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class AccountApiEmailVerificationTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();
    private readonly TestVerificationEmailService _testEmail = specification.TestEmail;

    [Fact]
    public async Task VerifyWithValidCodeShouldReturn200()
    {
        // Arrange
        var email = $"api-ev-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _testEmail.GetLastSentCodeFor(email);

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyWithNonexistentCodeShouldReturn400()
    {
        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/account/email-verification", new { code = "THISCODEDOESNOTEXIST99" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyWithAlreadyUsedCodeShouldReturn410()
    {
        // Arrange
        var email = $"api-ev-dup-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _testEmail.GetLastSentCodeFor(email);

        // Act
        await _client.PostAsJsonAsync("/api/account/email-verification", new { code });
        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task VerifyWithExpiredCodeShouldReturn410()
    {
        // Arrange
        var email = $"api-ev-exp-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(email);
        var code = _testEmail.GetLastSentCodeFor(email);

        using (var scope = specification._factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hashed = VerificationCodeHasher.Hash(code!);
            var entry = await db.VerificationCodes.SingleAsync(c => c.Code == hashed);
            entry.CreatedAt = DateTime.UtcNow.AddMinutes(-31);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Gone);
    }

    private async Task RegisterUserAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/account/register", new
        {
            email,
            password = "Pass123$",
            confirmPassword = "Pass123$"
        });
    }
}
