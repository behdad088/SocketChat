namespace Identity.API.Tests.OidcFlowTests;

[Collection(TestCollection.Name)]
public class LocalApiScopeTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task PasswordClientCanObtainTokenWithLocalApiScope()
    {
        // Act
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$",
            scopes: "openid profile IdentityServerApi");

        // Assert
        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }
}
