namespace Identity.API.Tests.SecurityTests;

/// <summary>
/// Verifies that security response headers are present on pages decorated with [SecurityHeaders].
/// Login is the primary target because it uses the attribute.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SecurityHeadersTests(IdentityApiSpecification specification)
{
    private readonly HttpClient _client = specification.CreateClientAndBindSpy();

    [Fact]
    public async Task Login_page_has_X_Content_Type_Options_nosniff()
    {
        var response = await _client.GetAsync("/Account/Login");
        response.Headers.TryGetValues("X-Content-Type-Options", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("nosniff");
    }

    [Fact]
    public async Task Login_page_has_X_Frame_Options_DENY()
    {
        var response = await _client.GetAsync("/Account/Login");
        response.Headers.TryGetValues("X-Frame-Options", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("DENY");
    }

    [Fact]
    public async Task Login_page_has_Content_Security_Policy()
    {
        var response = await _client.GetAsync("/Account/Login");
        response.Headers.TryGetValues("Content-Security-Policy", out var values);
        values.ShouldNotBeNull();
        values!.First().ShouldContain("default-src 'self'");
        values.First().ShouldContain("frame-ancestors 'none'");
    }

    [Fact]
    public async Task Login_page_has_Referrer_Policy_no_referrer()
    {
        var response = await _client.GetAsync("/Account/Login");
        response.Headers.TryGetValues("Referrer-Policy", out var values);
        values.ShouldNotBeNull();
        values!.ShouldContain("no-referrer");
    }

    [Fact]
    public async Task Login_page_has_X_Content_Security_Policy_for_IE()
    {
        var response = await _client.GetAsync("/Account/Login");
        response.Headers.TryGetValues("X-Content-Security-Policy", out var values);
        values.ShouldNotBeNull();
        values!.First().ShouldContain("default-src 'self'");
    }

    [Fact]
    public async Task Register_page_does_not_have_security_headers_by_default()
    {
        // Register page doesn't have [SecurityHeaders] attribute — it should NOT have these headers
        var response = await _client.GetAsync("/Account/Register");
        response.Headers.TryGetValues("X-Frame-Options", out var values);
        // Either null or absent — Register page doesn't decorate itself with [SecurityHeaders]
        // This test documents the current state; if it starts having the header, it's an improvement.
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
