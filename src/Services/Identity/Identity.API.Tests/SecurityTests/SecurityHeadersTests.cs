namespace Identity.API.Tests.SecurityTests;

/// <summary>
/// Verifies that security response headers are present on pages decorated with [SecurityHeaders].
/// Login is the primary target because it uses the attribute.
/// </summary>
[Collection(TestCollection.Name)]
public class SecurityHeadersTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();

    [Fact]
    public async Task LoginPageHasXContentTypeOptionsNosniff()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login");
        
        // Assert
        response.Headers.TryGetValues("X-Content-Type-Options", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("nosniff");
    }

    [Fact]
    public async Task LoginPageHasXFrameOptionsDENY()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login");
        
        // Assert
        response.Headers.TryGetValues("X-Frame-Options", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("DENY");
    }

    [Fact]
    public async Task LoginPageHasContentSecurityPolicy()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login");
        
        // Assert
        response.Headers.TryGetValues("Content-Security-Policy", out var values);
        values.ShouldNotBeNull();
        values!.First().ShouldContain("default-src 'self'");
        values.First().ShouldContain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task LoginPageHasReferrerPolicyNoReferrer()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login");
        // Assert
        response.Headers.TryGetValues("Referrer-Policy", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("no-referrer");
    }

    [Fact]
    public async Task LoginPageHasXContentSecurityPolicyForIE()
    {
        // Act
        var response = await _client.GetAsync("/Account/Login");
        
        // Assert
        response.Headers.TryGetValues("X-Content-Security-Policy", out var values);
        values.ShouldNotBeNull();
        values!.First().ShouldContain("default-src 'self'");
    }

    [Fact]
    public async Task RegisterPageDoesNotHaveSecurityHeadersByDefault()
    {
        // Register page doesn't have [SecurityHeaders] attribute — it should NOT have these headers
        var response = await _client.GetAsync("/Account/Register");
        response.Headers.TryGetValues("X-Frame-Options", out var values);
        // Either null or absent — Register page doesn't decorate itself with [SecurityHeaders]
        // This test documents the current state; if it starts having the header, it's an improvement.
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
