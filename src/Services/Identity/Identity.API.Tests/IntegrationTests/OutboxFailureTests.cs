using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;
using Identity.API.Models;
using Identity.API.Services.Account;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class OutboxFailureTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task OutboxRowStaysPendingAndRetriesWhenBrokerUnreachable()
    {
        await specification._factory!.DisposeAsync();

        try
        {
            await using var factory = new ApiFactory(specification.PostgresConnectionString, "rabbitmq://localhost:1");
            using var client = factory.CreateClient();

            var email = $"retry-{Guid.NewGuid():N}@test.com";
            var response = await client.PostAsJsonAsync("/api/account/register",
                new { email, password = Password, confirmPassword = Password });
            response.StatusCode.ShouldBe(HttpStatusCode.Created);

            OutboxMessage? row = null;
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                using var scope = factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                row = await db.OutboxMessages.AsNoTracking()
                    .SingleOrDefaultAsync(m => m.EventType == UserCreatedEvent.CloudEventType);
                if (row is { AttemptCount: >= 1 })
                {
                    break;
                }

                await Task.Delay(250);
            }

            row.ShouldNotBeNull();
            row.DispatchedAt.ShouldBeNull();
            row.AttemptCount.ShouldBeGreaterThanOrEqualTo(1);
            row.LastError.ShouldNotBeNullOrEmpty();
            row.NextAttemptAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        }
        finally
        {
            // Recreate the shared factory
            specification.CreateClientAndBindSpy();
        }
    }

    [Fact]
    public async Task RegisterRollsBackTheUserWhenTheOutboxWriteFails()
    {
        // Arrange
        specification.CreateClientAndBindSpy();

        var throwingWriter = Substitute.For<IOutboxWriter>();
        throwingWriter.Enqueue(Arg.Any<UserCreatedEvent>(), Arg.Any<DateTimeOffset>())
            .Returns<OutboxMessage>(_ => throw new InvalidOperationException("outbox write failed"));

        var email = $"rollback-{Guid.NewGuid():N}@test.com";

        // Act
        using (var scope = specification._factory!.Services.CreateScope())
        {
            var serviceProvider = scope.ServiceProvider;
            var service = new AccountService(
                serviceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
                serviceProvider.GetRequiredService<IVerificationEmailService>(),
                serviceProvider.GetRequiredService<ApplicationDbContext>(),
                throwingWriter);

            await Should.ThrowAsync<InvalidOperationException>(
                () => service.RegisterAsync(email, Password));
        }

        // Assert
        using var assertScope = specification._factory!.Services.CreateScope();
        var userManager = assertScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).ShouldBeNull();

        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.OutboxMessages.AsNoTracking().CountAsync()).ShouldBe(0);
    }
}
