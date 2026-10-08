using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Documents;
using Marten;
using Marten.Patching;

namespace Chat.IntegrationTests.Storage;

[Collection(StorageCollection.Name)]
public class ProfileDocumentTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Write_ThenLoad_ReturnsEveryField()
    {
        var profile = NewProfile(version: 3) with
        {
            DisplayName = "Countess",
            ProfilePicture = "https://example.com/test.png",
            IsActive = true,
            LastOnline = new DateTimeOffset(2026, 10, 7, 8, 30, 0, TimeSpan.Zero),
            Quote = "That brain of mine is something more than merely mortal."
        };

        await WriteAsync(profile);

        (await LoadAsync(profile.Id)).ShouldBe(profile);
    }

    [Fact]
    public async Task Write_WithOnlyRequiredFields_LeavesOptionalFieldsNull()
    {
        var profile = new ProfileDocument
        {
            Id = Guid.NewGuid().ToString(),
            Username = "test",
            Email = "test@example.com",
            Version = 1
        };

        await WriteAsync(profile);

        var loaded = await LoadAsync(profile.Id);
        loaded.ShouldNotBeNull();
        loaded.Firstname.ShouldBeNull();
        loaded.Lastname.ShouldBeNull();
        loaded.PhoneNumber.ShouldBeNull();
        loaded.DisplayName.ShouldBeNull();
        loaded.ProfilePicture.ShouldBeNull();
        loaded.IsActive.ShouldBeNull();
        loaded.LastOnline.ShouldBeNull();
        loaded.Quote.ShouldBeNull();
    }

    [Fact]
    public async Task Write_StoresJsonWithSchemaFieldNames()
    {
        var profile = NewProfile(version: 1);

        await WriteAsync(profile);

        var keys = await fixture.GetJsonKeysAsync("mt_doc_profiledocument", profile.Id);
        keys.ShouldBe(
        [
            "Id", "Username", "Firstname", "Lastname", "DisplayName", "Email", "PhoneNumber",
            "ProfilePicture", "IsActive", "LastOnline", "Quote", "IsDeleted", "DeletedAt", "Version"
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task UpdatingLastOnline_LeavesTheIdentityVersionUnchanged()
    {
        var profile = NewProfile(version: 3);
        await WriteAsync(profile);

        await using (var session = fixture.Store.LightweightSession())
        {
            session.Patch<ProfileDocument>(profile.Id)
                .Set(doc => doc.LastOnline, new DateTimeOffset(2026, 10, 7, 22, 0, 0, TimeSpan.Zero));
            await session.SaveChangesAsync();
        }

        (await LoadAsync(profile.Id))!.Version.ShouldBe(3);
    }

    private static ProfileDocument NewProfile(int version) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Username = "test",
        Firstname = "Test",
        Lastname = "TestLastname",
        Email = "test@example.com",
        PhoneNumber = "+46700000000",
        Version = version
    };

    private async Task WriteAsync(ProfileDocument profile)
    {
        await using var session = fixture.Store.LightweightSession();
        session.Store(profile);
        await session.SaveChangesAsync();
    }

    private async Task<ProfileDocument?> LoadAsync(string id)
    {
        await using var query = fixture.Store.QuerySession();
        return await query.LoadAsync<ProfileDocument>(id);
    }
}
