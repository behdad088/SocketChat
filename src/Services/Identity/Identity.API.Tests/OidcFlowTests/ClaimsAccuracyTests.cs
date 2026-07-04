namespace Identity.API.Tests.OidcFlowTests;

/// <summary>
/// Verifies that role-based permissions embedded in access tokens match the
/// policy definitions in Config.RolePolicyDefinitions.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ClaimsAccuracyTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task Alice_token_contains_customer_role()
    {
        var token = await GetAliceTokenAsync();
        var role = TokenHelper.ParseClaim(token, "role");
        role.ShouldBe("customer");
    }

    [Fact]
    public async Task Bob_token_contains_admin_role()
    {
        var token = await GetBobTokenAsync();
        var role = TokenHelper.ParseClaim(token, "role");
        role.ShouldBe("admin");
    }

    [Fact]
    public async Task Alice_token_contains_user_chat_read_and_write_permissions()
    {
        var token = await GetAliceTokenAsync();
        var permissions = TokenHelper.ParsePermissions(token).ToList();
        permissions.ShouldContain("chat:user-message:read");
        permissions.ShouldContain("chat:user-message:write");
    }

    [Fact]
    public async Task Token_contains_sub_claim()
    {
        var token = await GetAliceTokenAsync();
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
