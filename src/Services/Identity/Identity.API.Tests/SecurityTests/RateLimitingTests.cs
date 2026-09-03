using Microsoft.AspNetCore.Mvc.Testing;

namespace Identity.API.Tests.SecurityTests;

[Collection(TestCollection.Name)]
public class RateLimitingTests
{
    private readonly IdentityApiSpecification _specification;

    public RateLimitingTests(IdentityApiSpecification specification)
    {
        _specification = specification;
        _specification.CreateClientAndBindSpy(false);
    }

    [Fact]
    public async Task LoginReturnsTooManyRequestsAfterExceedingPermitLimit()
    {
        // Act
        var responses = await SendRequestsAsync("/Account/Login", count: 11);

        // Assert
        responses.Count(r => r == HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1,
            "Expected at least one 429 after exceeding the login rate limit");
    }

    [Fact]
    public async Task RegisterReturnsTooManyRequestsAfterExceedingPermitLimit()
    {
        // Act
        var responses = await SendRequestsAsync("/Account/Register", count: 6);

        // Assert
        responses.Count(r => r == HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ForgotPasswordReturnsTooManyRequestsAfterExceedingPermitLimit()
    {
        // Act
        var responses = await SendRequestsAsync("/Account/ForgotPassword", count: 6);

        // Assert
        responses.Count(r => r == HttpStatusCode.TooManyRequests)
            .ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task LoginReturnsOkForFirstRequest()
    {
        // Arrange
        var client = CreateClient();
        
        // Act
        var response = await client.GetAsync("/Account/Login");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<List<HttpStatusCode>> SendRequestsAsync(string path, int count)
    {
        var client = CreateClient();
        var results = new List<HttpStatusCode>();

        for (var i = 0; i < count; i++)
        {
            var response = await client.GetAsync(path);
            results.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                break;
        }

        return results;
    }

    private HttpClient CreateClient() =>
        _specification._factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
}
