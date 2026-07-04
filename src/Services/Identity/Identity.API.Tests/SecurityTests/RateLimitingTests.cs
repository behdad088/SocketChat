namespace Identity.API.Tests.SecurityTests;

[Collection(IntegrationTestCollection.Name)]
public class RateLimitingTests
{
    private readonly IdentityApiSpecification _specification;

    public RateLimitingTests(IdentityApiSpecification specification)
    {
        _specification = specification;
        _specification.CreateClientAndBindSpy(false); // ensures EmailSpy is wired up
    }

    [Fact]
    public async Task Login_returns_429_after_exceeding_permit_limit()
    {
        // Login policy: PermitLimit = 10, Window = 1 minute
        // Make 11 GET requests; the 11th should be rate-limited.
        var responses = await SendRequestsAsync("/Account/Login", count: 11);

        responses.Count(r => r == System.Net.HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1,
            "Expected at least one 429 after exceeding the login rate limit");
    }

    [Fact]
    public async Task Register_returns_429_after_exceeding_permit_limit()
    {
        // Register policy: PermitLimit = 5, Window = 1 minute
        var responses = await SendRequestsAsync("/Account/Register", count: 6);

        responses.Count(r => r == System.Net.HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ForgotPassword_returns_429_after_exceeding_permit_limit()
    {
        // ForgotPassword policy: PermitLimit = 5, Window = 1 minute
        var responses = await SendRequestsAsync("/Account/ForgotPassword", count: 6);

        responses.Count(r => r == System.Net.HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Login_returns_200_for_first_request()
    {
        // Sanity: rate limiter does not block the very first request
        var client = CreateClient();
        var response = await client.GetAsync("/Account/Login");
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    private async Task<List<System.Net.HttpStatusCode>> SendRequestsAsync(string path, int count)
    {
        var client = CreateClient();
        var results = new List<System.Net.HttpStatusCode>();

        for (var i = 0; i < count; i++)
        {
            var response = await client.GetAsync(path);
            results.Add(response.StatusCode);
            // Stop early once we see a 429
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                break;
        }

        return results;
    }

    private HttpClient CreateClient() =>
        _specification._factory!.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
}
