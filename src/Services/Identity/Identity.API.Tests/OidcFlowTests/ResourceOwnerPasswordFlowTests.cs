namespace Identity.API.Tests.OidcFlowTests;

[Collection(IntegrationTestCollection.Name)]
public class ResourceOwnerPasswordFlowTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task Alice_can_obtain_token_with_correct_credentials()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$");

        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Bob_can_obtain_token_with_correct_credentials()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "BobSmith@email.com", "Pass123$");

        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Wrong_password_returns_error()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "WrongPassword!");

        result.IsError.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Nonexistent_user_returns_error()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "nobody@example.com", "Pass123$");

        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task Token_endpoint_reachable_at_expected_path()
    {
        // Discovery document sanity check
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("token_endpoint");
    }
}
