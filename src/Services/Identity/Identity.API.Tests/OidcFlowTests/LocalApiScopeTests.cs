namespace Identity.API.Tests.OidcFlowTests;

[Collection(IntegrationTestCollection.Name)]
public class LocalApiScopeTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task Password_client_can_obtain_token_with_local_api_scope()
    {
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$",
            scopes: "openid profile IdentityServerApi");

        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }
}
