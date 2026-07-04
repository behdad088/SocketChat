namespace Identity.API.Tests.OidcFlowTests;

[Collection(IntegrationTestCollection.Name)]
public class TokenIntrospectionTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification._factory!.CreateClient();

    [Fact]
    public async Task Chat_service_can_introspect_valid_alice_token()
    {
        var tokenResult = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$", "openid profile chat");

        tokenResult.IsError.ShouldBeFalse();

        var introspectionResult = await TokenHelper.IntrospectTokenAsync(
            _client, tokenResult.AccessToken!, "chat", "chat-secret");

        introspectionResult.ShouldNotBeNull();
        introspectionResult!["active"]?.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task Introspection_returns_inactive_for_random_string()
    {
        var result = await TokenHelper.IntrospectTokenAsync(
            _client, "not-a-real-token", "chat", "chat-secret");

        result.ShouldNotBeNull();
        result!["active"]?.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task Introspection_with_wrong_resource_secret_returns_401()
    {
        var tokenResult = await TokenHelper.RequestPasswordTokenAsync(
            _client, "AliceSmith@email.com", "Pass123$", "openid profile chat");

        tokenResult.IsError.ShouldBeFalse();

        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        var badCredentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("chat:wrong-secret"));
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", badCredentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", tokenResult.AccessToken!)
        });

        var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task All_four_api_resources_can_introspect_with_their_own_secrets()
    {
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
