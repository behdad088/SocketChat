using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using Chat.Storage.Queries;

namespace Chat.IntegrationTests.Storage.Queries;

[Collection(StorageCollection.Name)]
public class GetProfilesQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetProfiles_ReturnsTheKnownProfilesAndSkipsUnknownIds()
    {
        var test = await CreateProfileAsync("test@example.com");
        var test2 = await CreateProfileAsync("test2@example.com");
        var unknown = Guid.NewGuid().ToString();

        var profiles = await ExecuteAsync([test, test2, unknown]);

        profiles.Select(profile => profile.Id).ShouldBe([test, test2], ignoreOrder: true);
    }

    [Fact]
    public async Task GetProfiles_SkipsDeletedUsers()
    {
        var ada = await CreateProfileAsync("ada@example.com");
        var deleted = await CreateProfileAsync("deleted@example.com");
        await using (var session = fixture.Store.LightweightSession())
        {
            await new DeleteUserDataCommand().Execute(session, new DeleteUserDataParameters(deleted, 2, DateTimeOffset.UtcNow), CancellationToken.None);
            await session.SaveChangesAsync();
        }

        var profiles = await ExecuteAsync([ada, deleted]);

        profiles.Select(profile => profile.Id).ShouldBe([ada]);
    }

    [Fact]
    public async Task GetProfiles_NoIds_ReturnsAnEmptyList()
    {
        var profiles = await ExecuteAsync([]);

        profiles.ShouldBeEmpty();
    }

    private async Task<string> CreateProfileAsync(string email)
    {
        var userId = Guid.NewGuid().ToString();
        await using var session = fixture.Store.LightweightSession();
        await new UpsertProfileCommand().Execute(session, new UpsertProfileParameters(
            userId, email, email, null, null, null, 1), CancellationToken.None);
        await session.SaveChangesAsync();
        return userId;
    }

    private async Task<IReadOnlyList<ProfileDocument>> ExecuteAsync(IReadOnlyCollection<string> userIds)
    {
        await using var query = fixture.Store.QuerySession();
        return await new GetProfilesQuery().Execute(query, new GetProfilesParameters(userIds), CancellationToken.None);
    }
}
