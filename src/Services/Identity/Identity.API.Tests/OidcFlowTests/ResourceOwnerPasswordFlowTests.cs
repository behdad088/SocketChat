namespace Identity.API.Tests.OidcFlowTests;

[Collection(TestCollection.Name)]
public class ResourceOwnerPasswordFlowTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task AliceCanObtainTokenWithCorrectCredentials()
    {
        // Arrange
        const string username = "AliceSmith@email.com";
        const string password = "Pass123$";
        
        // Act
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, username, password);

        // Assert
        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task BobCanObtainTokenWithCorrectCredentials()
    {
        // Arrange
        const string username = "BobSmith@email.com";
        const string password = "Pass123$";
        
        // Act
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, username, password);

        // Assert
        result.IsError.ShouldBeFalse(result.Error ?? "Unexpected error");
        result.AccessToken.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task WrongPasswordReturnsError()
    {
        // Arrange
        const string username = "BobSmith@email.com";
        const string password = "WrongPassword!";
        
        // Act
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, username, password);

        // Assert
        result.IsError.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NonexistentUserReturnsError()
    {
        // Arrange
        const string username = "nobody@example.com";
        const string password = "Pass123$";
        
        // Act
        var result = await TokenHelper.RequestPasswordTokenAsync(
            _client, username, password);

        // Assert
        result.IsError.ShouldBeTrue();
    }

    [Fact]
    public async Task TokenEndpointReachableAtExpectedPath()
    {
        // Act
        var response = await _client.GetAsync("/.well-known/openid-configuration");
        
        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("token_endpoint");
    }
}
