namespace Identity.API.Tests.OidcFlowTests;

[Collection(TestCollection.Name)]
public class TokenIntrospectionTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task ChatServiceCanIntrospectValidAliceToken()
    {
        // Arrange
        const string username = "AliceSmith@email.com";
        const string password = "Pass123$";
        const string scope = "openid profile chat";
        var tokenResult = await TokenHelper.RequestPasswordTokenAsync(
            _client, username, password, scope);

        tokenResult.IsError.ShouldBeFalse();

        // Act
        var introspectionResult = await TokenHelper.IntrospectTokenAsync(
            _client, tokenResult.AccessToken!, "chat", "chat-secret");

        // Assert
        introspectionResult.ShouldNotBeNull();
        introspectionResult!["active"]?.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task IntrospectionReturnsInactiveForRandomString()
    {
        // Act
        var result = await TokenHelper.IntrospectTokenAsync(
            _client, "not-a-real-token", "chat", "chat-secret");

        // Assert
        result.ShouldNotBeNull();
        result!["active"]?.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task IntrospectionWithWrongResourceSecretReturnsUnauthorized()
    {
        // Arrange
        var tokenResult = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$", "openid profile chat");

        tokenResult.IsError.ShouldBeFalse();

        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        var badCredentials = Convert.ToBase64String(
            "chat:wrong-secret"u8.ToArray());
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", badCredentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", tokenResult.AccessToken!)
        });

        // Act
        var response = await _client.SendAsync(request);
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AllFourApiResourcesCanIntrospectWithTheirOwnSecrets()
    {
        
        // Arrange
        var tokenResult = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$",
            "chat");

        tokenResult.IsError.ShouldBeFalse();

        var resources = new[]
        {
            ("chat", "chat-secret"),
        };

        foreach (var (name, secret) in resources)
        {
            var result = await TokenHelper.IntrospectTokenAsync(
                _client, tokenResult.AccessToken!, name, secret);
            result!["active"]?.GetValue<bool>().ShouldBeTrue(
                $"Resource '{name}' could not introspect a valid token");
        }
    }
}
