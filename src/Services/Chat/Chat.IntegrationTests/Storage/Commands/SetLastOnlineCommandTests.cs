using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class SetLastOnlineCommandTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset Earlier = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SetLastOnline_SetsItWithoutChangingVersion()
    {
        var userId = await CreateProfileAsync(version: 3);

        await SetLastOnlineAsync(userId, Later);

        var loaded = await LoadAsync(userId);
        loaded!.LastOnline.ShouldBe(Later);
        loaded.Version.ShouldBe(3);
    }

    [Fact]
    public async Task SetLastOnline_OlderTimestamp_IsIgnored()
    {
        var userId = await CreateProfileAsync(version: 1);
        await SetLastOnlineAsync(userId, Later);

        await SetLastOnlineAsync(userId, Earlier);

        (await LoadAsync(userId))!.LastOnline.ShouldBe(Later);
    }

    [Fact]
    public async Task SetLastOnline_UnknownUser_DoesNotCreateAProfile()
    {
        var userId = Guid.NewGuid().ToString();

        await SetLastOnlineAsync(userId, Later);

        (await LoadAsync(userId)).ShouldBeNull();
    }

    private async Task<string> CreateProfileAsync(int version)
    {
        var userId = Guid.NewGuid().ToString();
        await using var session = fixture.Store.LightweightSession();
        await new UpsertProfileCommand().Execute(session, new UpsertProfileParameters(
            userId, "test@example.com", "test@example.com", "Test", "TestPlace", null, version), CancellationToken.None);
        await session.SaveChangesAsync();
        return userId;
    }

    private async Task SetLastOnlineAsync(string userId, DateTimeOffset lastOnline)
    {
        await using var session = fixture.Store.LightweightSession();
        new SetLastOnlineCommand().Execute(session, new SetLastOnlineParameters(userId, lastOnline));
        await session.SaveChangesAsync();
    }

    private async Task<ProfileDocument?> LoadAsync(string id)
    {
        await using var query = fixture.Store.QuerySession();
        return await query.LoadAsync<ProfileDocument>(id);
    }
}
