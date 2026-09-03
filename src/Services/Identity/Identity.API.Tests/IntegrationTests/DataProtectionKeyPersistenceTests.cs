namespace Identity.API.Tests.IntegrationTests;

[Collection(TestCollection.Name)]
public class DataProtectionKeyPersistenceTests(IdentityApiSpecification specification)
{
    /// <summary>
    /// A GET to the login page triggers antiforgery token creation,
    /// which forces Data Protection to generate and persist a key
    /// </summary>
    [Fact]
    public async Task DataProtectionKeysArePersistedToDatabaseAfterFirstRequest()
    {
        // Arrange
        var client = specification._factory!.CreateClient();
        
        // Act
        await client.GetAsync("/Account/Login");

        // Assert
        await using var scope = specification._factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var keyCount = await db.DataProtectionKeys.CountAsync();
        keyCount.ShouldBeGreaterThan(0);
    }
}
