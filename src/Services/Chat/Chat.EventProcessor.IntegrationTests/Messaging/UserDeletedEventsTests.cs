using Chat.EventProcessor.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;
using static Chat.EventProcessor.IntegrationTests.Infrastructure.IdentityEventPublisher;

namespace Chat.EventProcessor.IntegrationTests.Messaging;

[Collection(EventProcessorCollection.Name)]
public class UserDeletedEventsTests(EventProcessorFixture fixture)
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UserDeleted_ReplacesTheProfileWithATombstone()
    {
        var user = UserEventData.New(version: 0);
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserDeletedAsync(
            new UserDeletedEventData(user.Id, Version: 1, DeletedAt)));

        (await fixture.LoadProfileAsync(user.Id)).ShouldBe(new ProfileDocument
        {
            Id = user.Id,
            Username = string.Empty,
            Email = string.Empty,
            IsDeleted = true,
            DeletedAt = DeletedAt,
            Version = 1
        });
    }

    [Fact]
    public async Task UserCreated_DeliveredAfterUserDeleted_IsIgnored()
    {
        var user = UserEventData.New(version: 0);
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserDeletedAsync(
            new UserDeletedEventData(user.Id, Version: 1, DeletedAt)));

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));

        var profile = await fixture.LoadProfileAsync(user.Id);
        profile.ShouldNotBeNull();
        profile.IsDeleted.ShouldBeTrue();
        profile.Email.ShouldBeEmpty();
        profile.Version.ShouldBe(1);
    }

    public static TheoryData<string, object?> InvalidUserDeletedEvents => new()
    {
        { UserDeletedType, new UserDeletedEventData("not-a-guid", Version: 1, DeletedAt) },
        { UserDeletedType, new UserDeletedEventData(Guid.NewGuid().ToString(), Version: 0, DeletedAt) },
        { UserDeletedType, new UserDeletedEventData(Guid.NewGuid().ToString(), Version: 1, default) },
        { UserDeletedType, null },
        { UserCreatedType, new UserDeletedEventData(Guid.NewGuid().ToString(), Version: 1, DeletedAt) }
    };

    [Theory]
    [MemberData(nameof(InvalidUserDeletedEvents))]
    public async Task InvalidUserDeleted_IsMovedToTheErrorQueueWithoutRetries(string type, object? data)
    {
        var messageId = await fixture.Identity.PublishAsync(UserDeletedExchange, type, data);

        (await fixture.Identity.WaitForErrorQueueAsync("chat.identity.user.deleted", messageId))
            .ShouldNotBeNull().ShouldNotContainKey(RetryCountHeader);
    }
}
