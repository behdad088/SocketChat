using Chat.IntegrationTests.Infrastructure;
using Chat.Storage.Commands;
using Chat.Storage.Documents;
using Marten;

namespace Chat.IntegrationTests.Storage.Commands;

[Collection(StorageCollection.Name)]
public class DeleteUserDataCommandTests(PostgresFixture fixture)
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Delete_ReplacesTheProfileWithATombstoneWithoutPersonalData()
    {
        var userId = await CreateProfileAsync(version: 3);
        await SetLastOnlineAsync(userId);

        await DeleteAsync(userId, version: 4);

        (await LoadProfileAsync(userId)).ShouldBe(new ProfileDocument
        {
            Id = userId,
            Username = string.Empty,
            Email = string.Empty,
            IsDeleted = true,
            DeletedAt = DeletedAt,
            Version = 4
        });
    }

    [Fact]
    public async Task Delete_KeepsTheChannelRows()
    {
        var userId = await CreateProfileAsync(version: 1);
        var peerId = Guid.NewGuid().ToString();
        var conversationId = Ulid.NewUlid().ToString();
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Insert(NewChannel(userId, peerId, conversationId));
            session.Insert(NewChannel(peerId, userId, conversationId));
            await session.SaveChangesAsync();
        }

        await DeleteAsync(userId, version: 2);

        await using var query = fixture.Store.QuerySession();
        (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(userId, peerId))).ShouldNotBeNull();
        (await query.LoadAsync<UserChannelDocument>(UserChannelDocument.CreateId(peerId, userId))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_KeepsConversationsAndMessages()
    {
        var userId = await CreateProfileAsync(version: 1);
        var conversation = new ConversationDocument
        {
            Id = Ulid.NewUlid().ToString(),
            Participants = [userId, Guid.NewGuid().ToString()],
            CreatedAt = DeletedAt.AddDays(-1)
        };
        var message = new MessageDocument
        {
            Id = Ulid.NewUlid().ToString(),
            ConversationId = conversation.Id,
            SenderId = userId,
            Content = "hello",
            CreatedAt = DeletedAt.AddDays(-1)
        };
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Insert(conversation);
            session.Insert(message);
            await session.SaveChangesAsync();
        }

        await DeleteAsync(userId, version: 2);

        await using var query = fixture.Store.QuerySession();
        (await query.LoadAsync<ConversationDocument>(conversation.Id)).ShouldNotBeNull();
        (await query.LoadAsync<MessageDocument>(message.Id))!.Content.ShouldBe("hello");
    }

    [Fact]
    public async Task Delete_Twice_IsANoOp()
    {
        var userId = await CreateProfileAsync(version: 1);
        (await DeleteAsync(userId, version: 2)).ShouldBeOfType<DeleteUserDataResult.Applied>();

        (await DeleteAsync(userId, version: 2)).ShouldBeOfType<DeleteUserDataResult.Ignored>();

        var loaded = await LoadProfileAsync(userId);
        loaded!.IsDeleted.ShouldBeTrue();
        loaded.Version.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertAfterDelete_WithAnOlderVersion_DoesNotBringThePersonalDataBack()
    {
        var userId = await CreateProfileAsync(version: 1);
        await DeleteAsync(userId, version: 3);

        await UpsertAsync(userId, version: 2);

        var loaded = await LoadProfileAsync(userId);
        loaded!.IsDeleted.ShouldBeTrue();
        loaded.Email.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_BeforeTheUserWasEverCreated_LeavesATombstoneThatIgnoresTheLateCreate()
    {
        var userId = Guid.NewGuid().ToString();

        await DeleteAsync(userId, version: 1);
        await UpsertAsync(userId, version: 0);

        var loaded = await LoadProfileAsync(userId);
        loaded!.IsDeleted.ShouldBeTrue();
        loaded.Email.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetLastOnline_AfterDelete_IsIgnored()
    {
        var userId = await CreateProfileAsync(version: 1);
        await DeleteAsync(userId, version: 2);

        await SetLastOnlineAsync(userId);

        (await LoadProfileAsync(userId))!.LastOnline.ShouldBeNull();
    }

    private static UserChannelDocument NewChannel(string userId, string peerId, string conversationId) => new()
    {
        Id = UserChannelDocument.CreateId(userId, peerId),
        ConversationId = conversationId,
        UserId = userId
    };

    private async Task<string> CreateProfileAsync(int version)
    {
        var userId = Guid.NewGuid().ToString();
        await UpsertAsync(userId, version);
        return userId;
    }

    private async Task UpsertAsync(string userId, int version)
    {
        await using var session = fixture.Store.LightweightSession();
        await new UpsertProfileCommand().Execute(
            session, 
            new UpsertProfileParameters(
                userId,
                "test@example.com",
                "test@example.com",
                "test",
                "test",
                null,
                version),
            CancellationToken.None);
        await session.SaveChangesAsync();
    }

    private async Task SetLastOnlineAsync(string userId)
    {
        await using var session = fixture.Store.LightweightSession();
        new SetLastOnlineCommand().Execute(session, new SetLastOnlineParameters(userId, DeletedAt.AddHours(-1)));
        await session.SaveChangesAsync();
    }

    private async Task<DeleteUserDataResult> DeleteAsync(string userId, int version)
    {
        await using var session = fixture.Store.LightweightSession();
        var result = await new DeleteUserDataCommand().Execute(
            session, new DeleteUserDataParameters(userId, version, DeletedAt), CancellationToken.None);
        await session.SaveChangesAsync();
        return result;
    }

    private async Task<ProfileDocument?> LoadProfileAsync(string id)
    {
        await using var query = fixture.Store.QuerySession();
        return await query.LoadAsync<ProfileDocument>(id);
    }
}
