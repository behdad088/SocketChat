using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using JasperFx;
using Marten;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class UpsertProfileCommandTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset LastOnline = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Upsert_UnknownUser_InsertsTheProfile()
    {
        var parameters = NewParameters(version: 0);

        var result = await UpsertAsync(parameters);

        result.ShouldBeOfType<UpsertProfileResult.Applied>();
        (await LoadAsync(parameters.UserId)).ShouldBe(new ProfileDocument
        {
            Id = parameters.UserId,
            Username = parameters.Username,
            Email = parameters.Email,
            Firstname = parameters.Firstname,
            Lastname = parameters.Lastname,
            ProfilePicture = parameters.ProfilePicture,
            Version = 0
        });
    }

    [Fact]
    public async Task Upsert_NewerVersion_ReplacesTheProfile()
    {
        var stored = NewParameters(version: 3);
        await UpsertAsync(stored);
        var newer = stored with { Username = "test.t", Firstname = "TestFirstname", Version = 4 };

        var result = await UpsertAsync(newer);

        result.ShouldBeOfType<UpsertProfileResult.Applied>();
        var loaded = await LoadAsync(stored.UserId);
        loaded!.Username.ShouldBe("test.t");
        loaded.Firstname.ShouldBe("TestFirstname");
        loaded.Version.ShouldBe(4);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public async Task Upsert_SameOrOlderVersion_IsIgnored(int incomingVersion)
    {
        var stored = NewParameters(version: 3);
        await UpsertAsync(stored);

        var result = await UpsertAsync(stored with { Username = "outdated", Version = incomingVersion });

        result.ShouldBeOfType<UpsertProfileResult.Ignored>();
        var loaded = await LoadAsync(stored.UserId);
        loaded!.Username.ShouldBe(stored.Username);
        loaded.Version.ShouldBe(3);
    }

    [Fact]
    public async Task Upsert_KeepsTheStoredLastOnline()
    {
        var stored = NewParameters(version: 3);
        await UpsertAsync(stored);
        await SetLastOnlineAsync(stored.UserId);

        await UpsertAsync(stored with { Username = "test.t", Version = 4 });

        var loaded = await LoadAsync(stored.UserId);
        loaded!.Username.ShouldBe("test.t");
        loaded.LastOnline.ShouldBe(LastOnline);
    }

    [Fact]
    public async Task Upsert_BlankOptionalFields_AreStoredAsNull()
    {
        var parameters = NewParameters(version: 0) with { Firstname = "", Lastname = "  ", ProfilePicture = "" };

        await UpsertAsync(parameters);

        var loaded = await LoadAsync(parameters.UserId);
        loaded!.Firstname.ShouldBeNull();
        loaded.Lastname.ShouldBeNull();
        loaded.ProfilePicture.ShouldBeNull();
    }

    [Fact]
    public async Task Upsert_NonLatinText_RoundTrips()
    {
        var parameters = NewParameters(version: 0) with { Firstname = "بهداد", Lastname = "کاردگر 👋" };

        await UpsertAsync(parameters);

        var loaded = await LoadAsync(parameters.UserId);
        loaded!.Firstname.ShouldBe("بهداد");
        loaded.Lastname.ShouldBe("کاردگر 👋");
    }

    [Fact]
    public async Task Upsert_FromAStaleRead_IsRejected()
    {
        var stored = NewParameters(version: 1);
        await UpsertAsync(stored);
        await using var slow = fixture.Store.LightweightSession();
        await new UpsertProfileCommand().Execute(slow, stored with { Version = 3 }, CancellationToken.None);

        await UpsertAsync(stored with { Version = 2 });

        await Should.ThrowAsync<ConcurrencyException>(() => slow.SaveChangesAsync());
    }

    [Fact]
    public async Task Upsert_AfterAConcurrentLastOnlineWrite_IsRejectedSoLastOnlineIsNotLost()
    {
        var stored = NewParameters(version: 1);
        await UpsertAsync(stored);
        await using var slow = fixture.Store.LightweightSession();
        await new UpsertProfileCommand().Execute(slow, stored with { Version = 2 }, CancellationToken.None);

        await SetLastOnlineAsync(stored.UserId);

        await Should.ThrowAsync<ConcurrencyException>(() => slow.SaveChangesAsync());
    }

    [Fact]
    public async Task Upsert_ConcurrentOutOfOrderRedeliveries_KeepTheHighestVersion()
    {
        var userId = Guid.NewGuid().ToString();
        var versions = Enumerable.Range(1, 30).OrderBy(_ => Random.Shared.Next()).ToList();

        await Task.WhenAll(versions.Select(version => fixture.SaveWithRetryAsync(session =>
            new UpsertProfileCommand().Execute(
                session,
                NewParameters(version) with { UserId = userId, Username = $"v{version}" },
                CancellationToken.None))));

        var loaded = await LoadAsync(userId);
        loaded!.Version.ShouldBe(30);
        loaded.Username.ShouldBe("v30");
    }

    private static UpsertProfileParameters NewParameters(int version) => new(
        UserId: Guid.NewGuid().ToString(),
        Username: "test@example.com",
        Email: "test@example.com",
        Firstname: "Test",
        Lastname: "TestLastname",
        ProfilePicture: "https://example.com/test.png",
        Version: version);

    private async Task<UpsertProfileResult> UpsertAsync(UpsertProfileParameters parameters)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new UpsertProfileCommand().Execute(session, parameters, CancellationToken.None);
        await session.SaveChangesAsync();
        return result;
    }

    private async Task SetLastOnlineAsync(string userId)
    {
        await using var session = fixture.Store.LightweightSession();
        new SetLastOnlineCommand().Execute(session, new SetLastOnlineParameters(userId, LastOnline));
        await session.SaveChangesAsync();
    }

    private async Task<ProfileDocument?> LoadAsync(string id)
    {
        await using var query = fixture.Store.QuerySession();
        return await query.LoadAsync<ProfileDocument>(id);
    }
}
