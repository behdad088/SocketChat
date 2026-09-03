using Identity.API.Models;
using Identity.API.Tests.OidcFlowTests;
using Microsoft.AspNetCore.Identity;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class UserEventsTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();

    private static readonly TimeSpan MessageTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task RegisterPublishesUserCreatedCloudEvent()
    {
        // Arrange
        await using var consumer = await RabbitMqTestConsumer.CreateAsync(
            specification.RabbitMqAmqpUri,
            "identity.user.created");

        var email = $"evt-created-{Guid.NewGuid():N}@test.com";
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/account/register",
            new { email, password = Password, confirmPassword = Password });
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var message = await consumer.WaitForMessageAsync(MessageTimeout);
        message.ShouldNotBeNull("expected a CloudEvent on identity.user.created");
        var envelope = message!.Value;

        envelope.GetProperty("specversion").GetString().ShouldBe("1.0");
        envelope.GetProperty("type").GetString().ShouldBe("com.socketchat.identity.user.created");
        envelope.GetProperty("source").GetString().ShouldBe("urn:socketchat:identity-api");
        envelope.GetProperty("id").GetString().ShouldNotBeNullOrEmpty();
        envelope.GetProperty("datacontenttype").GetString().ShouldBe("application/json");
        envelope.GetProperty("traceparent").GetString().ShouldNotBeNullOrEmpty();

        var data = envelope.GetProperty("data");
        data.GetProperty("email").GetString().ShouldBe(email);
        data.GetProperty("username").GetString().ShouldBe(email);
        data.GetProperty("email_confirmed").GetBoolean().ShouldBeFalse();
        data.GetProperty("version").GetInt32().ShouldBe(0);
        data.GetProperty("id").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpdateProfilePublishesUserUpdatedCloudEvent()
    {
        // Arrange
        await using var consumer = await RabbitMqTestConsumer.CreateAsync(
            specification.RabbitMqAmqpUri,
            "identity.user.updated");

        var email = await CreateUserViaUserManagerAsync();
        var token = await GetTokenAsync(email);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/account/profile")
        {
            Content = JsonContent.Create(new { name = "Updated", lastName = "Person", profilePicture = "" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"0\""));

        // Act
        var response = await _client.SendAsync(request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var message = await consumer.WaitForMessageAsync(MessageTimeout);
        message.ShouldNotBeNull("expected a CloudEvent on identity.user.updated");
        var envelope = message.Value;

        envelope.GetProperty("type").GetString().ShouldBe("com.socketchat.identity.user.updated");

        var data = envelope.GetProperty("data");
        data.GetProperty("email").GetString().ShouldBe(email);
        data.GetProperty("name").GetString().ShouldBe("Updated");
        data.GetProperty("last_name").GetString().ShouldBe("Person");
        data.GetProperty("version").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task VerifyEmailPublishesUserUpdatedCloudEvent()
    {
        // Arrange
        await using var consumer = await RabbitMqTestConsumer.CreateAsync(
            specification.RabbitMqAmqpUri, 
            "identity.user.updated");

        var email = $"evt-verify-{Guid.NewGuid():N}@test.com";
        
        // Act
        var register = await _client.PostAsJsonAsync("/api/account/register",
            new { email, password = Password, confirmPassword = Password });
        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var code = specification.TestEmail.GetLastSentCodeFor(email);
        var verify = await _client.PostAsJsonAsync("/api/account/email-verification", new { code });
        verify.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Assert
        var message = await consumer.WaitForMessageAsync(MessageTimeout);
        message.ShouldNotBeNull("expected a CloudEvent on identity.user.updated");
        var envelope = message!.Value;

        envelope.GetProperty("type").GetString().ShouldBe("com.socketchat.identity.user.updated");

        var data = envelope.GetProperty("data");
        data.GetProperty("email").GetString().ShouldBe(email);
        data.GetProperty("email_confirmed").GetBoolean().ShouldBeTrue();
        data.GetProperty("version").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ResetPasswordPublishesNoEvent()
    {
        // Arrange
        specification.TestEmail.Reset();
        await _client.PostAsJsonAsync("/api/account/forgot-password",
            new { email = "AliceSmith@email.com" });
        var code = specification.TestEmail.GetLastSentCodeFor("AliceSmith@email.com");
        code.ShouldNotBeNullOrEmpty();

        using var scope = specification._factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hashed = VerificationCodeHasher.Hash(code);
        var aliceUserId = (await db.VerificationCodes.SingleAsync(c => c.Code == hashed)).UserId;

        // Act
        var response = await _client.PostAsJsonAsync("/api/account/reset-password", new
        {
            userId = aliceUserId,
            code,
            password = "NewPass999!",
            confirmPassword = "NewPass999!"
        });
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await db.OutboxMessages.AsNoTracking().CountAsync()).ShouldBe(0);
    }

    private async Task<string> CreateUserViaUserManagerAsync()
    {
        var email = $"evt-profile-{Guid.NewGuid():N}@test.com";
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
}
