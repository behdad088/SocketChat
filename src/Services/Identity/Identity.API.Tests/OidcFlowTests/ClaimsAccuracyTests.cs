namespace Identity.API.Tests.OidcFlowTests;

/// <summary>
/// Verifies that role-based permissions embedded in access tokens match the
/// policy definitions in Config.RolePolicyDefinitions.
/// </summary>
[Collection(TestCollection.Name)]
public class ClaimsAccuracyTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task AliceTokenContainsCustomerRole()
    {
        // Arrange
        var token = await GetAliceTokenAsync();
        
        // Act
        var role = TokenHelper.ParseClaim(token, "role");
        
        // Assert
        role.ShouldBe("customer");
    }

    [Fact]
    public async Task BobTokenContainsAdminRole()
    {
        // Arrange
        var token = await GetBobTokenAsync();
        
        // Act
        var role = TokenHelper.ParseClaim(token, "role");
        
        // Assert
        role.ShouldBe("admin");
    }

    [Fact]
    public async Task AliceTokenContainsUserChatReadAndWritePermissions()
    {
        // Arrange
        var token = await GetAliceTokenAsync();
        
        // Act
        var permissions = TokenHelper.ParsePermissions(token).ToList();
        
        // Assert
        permissions.ShouldContain("chat:user-message:read");
        permissions.ShouldContain("chat:user-message:write");
    }

    [Fact]
    public async Task TokenContainsSubClaim()
    {
        // Act
        var token = await GetAliceTokenAsync();
        
        // Assert
        var sub = TokenHelper.ParseClaim(token, "sub");
        sub.ShouldNotBeNullOrEmpty();
    }

    private async Task<string> GetAliceTokenAsync()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$");
        result.IsError.ShouldBeFalse();
        return result.AccessToken!;
    }

    private async Task<string> GetBobTokenAsync()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "BobSmith@email.com", "Pass123$");
        result.IsError.ShouldBeFalse();
        return result.AccessToken!;
    }
}
