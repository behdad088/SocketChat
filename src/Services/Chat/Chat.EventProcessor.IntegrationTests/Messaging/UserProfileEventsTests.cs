using Chat.EventProcessor.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using static Chat.EventProcessor.IntegrationTests.Infrastructure.IdentityEventPublisher;

namespace Chat.EventProcessor.IntegrationTests.Messaging;

[Collection(EventProcessorCollection.Name)]
public class UserProfileEventsTests(EventProcessorFixture fixture)
{
    private static readonly DateTimeOffset LastOnline = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task UserCreated_StoresTheProfile()
    {
        var user = UserEventData.New(version: 0);

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));

        (await fixture.LoadProfileAsync(user.Id)).ShouldBe(new ProfileDocument
        {
            Id = user.Id,
            Username = "test.user",
            Email = "test@test.com",
            Firstname = "Test",
            Lastname = "TestLastname",
            ProfilePicture = "https://test.com/test.png",
            Version = 0
        });
    }

    [Fact]
    public async Task UserUpdated_ReplacesTheProfile()
    {
        var user = UserEventData.New(version: 0);
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserUpdatedAsync(user with
        {
            Username = "renamed.user",
            Email = "renamed@test.com",
            Name = "Renamed",
            LastName = "RenamedLastname",
            ProfilePicture = "https://test.com/renamed.png",
            Version = 1
        }));
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserUpdatedAsync(
            user with { Name = "Renamed", LastName = null, ProfilePicture = "", Version = 2 }));

        var profile = await fixture.LoadProfileAsync(user.Id);
        profile.ShouldNotBeNull();
        profile.Firstname.ShouldBe("Renamed");
        profile.Lastname.ShouldBeNull();
        profile.ProfilePicture.ShouldBeNull();
        profile.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UserUpdated_OlderThanTheStoredProfile_IsIgnored()
    {
        var user = UserEventData.New(version: 2);
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserUpdatedAsync(user));

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserUpdatedAsync(
            user with { Name = "Outdated", Version = 1 }));

        var profile = await fixture.LoadProfileAsync(user.Id);
        profile.ShouldNotBeNull();
        profile.Firstname.ShouldBe("Test");
        profile.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UserUpdated_RacingALastOnlineWrite_IsRetriedAndKeepsLastOnline()
    {
        var user = UserEventData.New(version: 0);
        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));
        fixture.Conflicts.BeforeNextSave(user.Id, async store =>
        {
            await using var session = store.LightweightSession();
            new SetLastOnlineCommand().Execute(session, new SetLastOnlineParameters(user.Id, LastOnline));
            await session.SaveChangesAsync();
        });

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserUpdatedAsync(
            user with { Name = "Renamed", Version = 1 }));

        var profile = await fixture.LoadProfileAsync(user.Id);
        profile.ShouldNotBeNull();
        profile.Firstname.ShouldBe("Renamed");
        profile.Version.ShouldBe(1);
        profile.LastOnline.ShouldBe(LastOnline);
    }

    [Fact]
    public async Task UserCreated_RacingADuplicateDelivery_IsRetriedAndAcknowledged()
    {
        var user = UserEventData.New(version: 0);
        fixture.Conflicts.BeforeNextSave(user.Id, async store =>
        {
            await using var session = store.LightweightSession();
            await new UpsertProfileCommand().Execute(
                session,
                new UpsertProfileParameters(
                    user.Id, user.Username, user.Email, user.Name, user.LastName, user.ProfilePicture, user.Version),
                CancellationToken.None);
            await session.SaveChangesAsync();
        });

        await fixture.Consumed.WaitAsync(await fixture.Identity.PublishUserCreatedAsync(user));

        (await fixture.LoadProfileAsync(user.Id)).ShouldNotBeNull().Version.ShouldBe(0);
    }

    [Fact]
    public async Task ConcurrentOutOfOrderUpdates_KeepTheHighestVersion()
    {
        var user = UserEventData.New(version: 0);
        var versions = Enumerable.Range(1, 20).OrderBy(_ => Random.Shared.Next()).ToList();

        var messageIds = new List<Guid>();
        foreach (var version in versions)
            messageIds.Add(await fixture.Identity.PublishUserUpdatedAsync(
                user with { Name = $"v{version}", Version = version }));
        await Task.WhenAll(messageIds.Select(fixture.Consumed.WaitAsync));

        var profile = await fixture.LoadProfileAsync(user.Id);
        profile.ShouldNotBeNull();
        profile.Version.ShouldBe(20);
        profile.Firstname.ShouldBe("v20");
    }

    public static TheoryData<string, object?> InvalidUserCreatedEvents => new()
    {
        { UserCreatedType, UserEventData.New(version: 0) with { Id = "not-a-guid" } },
        { UserCreatedType, UserEventData.New(version: 0) with { Email = "" } },
        { UserCreatedType, UserEventData.New(version: 0) with { Username = "" } },
        { UserCreatedType, UserEventData.New(version: -1) },
        { UserCreatedType, null },
        { UserUpdatedType, UserEventData.New(version: 0) }
    };

    [Theory]
    [MemberData(nameof(InvalidUserCreatedEvents))]
    public async Task InvalidUserCreated_IsMovedToTheErrorQueueWithoutRetries(string type, object? data)
    {
        var messageId = await fixture.Identity.PublishAsync(UserCreatedExchange, type, data);

        (await fixture.Identity.WaitForErrorQueueAsync("chat.identity.user.created", messageId))
            .ShouldNotBeNull().ShouldNotContainKey(RetryCountHeader);
    }

    [Fact]
    public async Task InvalidUserCreated_IsNotPublishedAsAFault()
    {
        // Every Fault<T> MassTransit publishes also reaches the MassTransit:Fault exchange.
        var faults = await fixture.Identity.BindQueueAsync("MassTransit:Fault");

        var messageId = await fixture.Identity.PublishAsync(
            UserCreatedExchange, UserCreatedType, UserEventData.New(version: 0) with { Email = "" });
        (await fixture.Identity.WaitForErrorQueueAsync("chat.identity.user.created", messageId)).ShouldNotBeNull();

        (await fixture.Identity.CountMessagesAsync(faults)).ShouldBe(0u);
    }

    [Fact]
    public async Task UserCreated_ThatIsNotJson_IsMovedToTheErrorQueueWithoutRetries()
    {
        var messageId = await fixture.Identity.PublishBodyAsync(UserCreatedExchange, "not json");

        (await fixture.Identity.WaitForErrorQueueAsync("chat.identity.user.created", messageId))
            .ShouldNotBeNull().ShouldNotContainKey(RetryCountHeader);
    }
}
