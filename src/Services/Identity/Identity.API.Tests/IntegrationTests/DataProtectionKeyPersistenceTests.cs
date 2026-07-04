using Microsoft.EntityFrameworkCore;

namespace Identity.API.Tests.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class DataProtectionKeyPersistenceTests(IdentityApiSpecification specification)
{
    /// <summary>
    /// A GET to the login page triggers antiforgery token creation, which forces Data Protection to generate and persist a key
    /// </summary>
    [Fact]
    public async Task DataProtection_keys_are_persisted_to_database_after_first_request()
    {
        var client = specification._factory!.CreateClient();
        await client.GetAsync("/Account/Login");

        await using var scope = specification._factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var keyCount = await db.DataProtectionKeys.CountAsync();
        keyCount.ShouldBeGreaterThan(0);
    }
}
