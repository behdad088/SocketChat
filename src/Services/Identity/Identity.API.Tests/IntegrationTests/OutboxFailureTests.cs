using System.Net;
using System.Net.Http.Json;
using Identity.API.Messaging.Events;
using Identity.API.Messaging.Outbox;
using Identity.API.Models;
using Identity.API.Services.Account;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class OutboxFailureTests(IdentityApiSpecification specification)
{
    private const string Password = "Pass123$";

    [Fact]
    public async Task Outbox_row_stays_pending_and_retries_when_broker_unreachable()
    {
        // The shared factory left running by a previous test class has a
        // healthy dispatcher connected to the real broker and polling the SAME
        // Postgres DB — it would publish our row (setting DispatchedAt, with
        // AttemptCount staying 0) before the dead-broker dispatcher ever fails
        // at it. Stop it so only the dead-broker factory dispatches.
        specification._factory?.Dispose();

        try
        {
            // Fresh factory pointing at a port where nothing listens. Publishes
            // time out after Outbox__PublishTimeoutSeconds (2s in tests) and the
            // row gets a 30s backoff, so it must still be pending afterwards.
            using var factory = new ApiFactory(specification.PostgresConnectionString, "rabbitmq://localhost:1");
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
            row!.DispatchedAt.ShouldBeNull();
            row.AttemptCount.ShouldBeGreaterThanOrEqualTo(1);
            row.LastError.ShouldNotBeNullOrEmpty();
            row.NextAttemptAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        }
        finally
        {
            // Recreate the shared factory and re-point the process env at the
            // real containers for whichever test class runs next (several use
            // specification._factory directly without recreating it).
            specification.CreateClientAndBindSpy();
        }
    }

    [Fact]
    public async Task Register_rolls_back_the_user_when_the_outbox_write_fails()
    {
        // Re-point the process env at the shared containers (the previous test
        // may have run first and left RabbitMQ__Uri pointing at the dead port).
        specification.CreateClientAndBindSpy();

        var throwingWriter = Substitute.For<IOutboxWriter>();
        throwingWriter.Enqueue(Arg.Any<UserCreatedEvent>(), Arg.Any<DateTimeOffset>())
            .Returns<OutboxMessage>(_ => throw new InvalidOperationException("outbox write failed"));

        var email = $"rollback-{Guid.NewGuid():N}@test.com";

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

        // Assert from a fresh scope: the user created before the outbox write
        // must have been rolled back with it.
        using var assertScope = specification._factory!.Services.CreateScope();
        var userManager = assertScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await userManager.FindByEmailAsync(email)).ShouldBeNull();

        var db = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.OutboxMessages.AsNoTracking().CountAsync()).ShouldBe(0);
    }
}
