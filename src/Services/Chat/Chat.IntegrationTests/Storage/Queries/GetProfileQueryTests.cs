using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Queries;

namespace Chat.IntegrationTests.Storage.Queries;

[Collection(StorageCollection.Name)]
public class GetProfileQueryTests(PostgresFixture fixture)
{
    [Fact]
    public async Task GetProfile_ExistingUser_ReturnsTheProfile()
    {
        var userId = Guid.NewGuid().ToString();
        await using (var session = fixture.Store.LightweightSession())
        {
            await new UpsertProfileCommand().Execute(session, new UpsertProfileParameters(
                userId, "test@example.com", "test@example.com", "Test", "test", null, 2), CancellationToken.None);
            await session.SaveChangesAsync();
        }

        var result = await ExecuteAsync(userId);

        var success = result.ShouldBeOfType<GetProfileResult.Success>();
        success.Profile.Id.ShouldBe(userId);
        success.Profile.Firstname.ShouldBe("Test");
        success.Profile.Version.ShouldBe(2);
    }

    [Fact]
    public async Task GetProfile_UnknownUser_ReturnsNotFound()
    {
        var result = await ExecuteAsync(Guid.NewGuid().ToString());

        result.ShouldBeOfType<GetProfileResult.NotFound>();
    }

    [Fact]
    public async Task GetProfile_DeletedUser_ReturnsNotFound()
    {
        var userId = Guid.NewGuid().ToString();
        await using (var session = fixture.Store.LightweightSession())
        {
            await new DeleteUserDataCommand().Execute(session, new DeleteUserDataParameters(userId, 1, DateTimeOffset.UtcNow), CancellationToken.None);
            await session.SaveChangesAsync();
        }

        var result = await ExecuteAsync(userId);

        result.ShouldBeOfType<GetProfileResult.NotFound>();
    }

    private async Task<GetProfileResult> ExecuteAsync(string userId)
    {
        await using var query = fixture.Store.QuerySession();
        return await new GetProfileQuery().Execute(query, new GetProfileParameters(userId), CancellationToken.None);
    }
}
