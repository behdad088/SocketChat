using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.Tests.Infrastructure;

public class IdentityApiSpecification : IAsyncLifetime
{
    private readonly WebApiContainerFactory _webApiContainer = new();
    public ApiFactory? _factory;
    private string? _postgresConnectionString;
    public FakeVerificationEmailService EmailSpy { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _webApiContainer.InitializeAsync();
        _postgresConnectionString = _webApiContainer.PostgresConnectionString;
        
        _factory = new ApiFactory(
            _webApiContainer.PostgresConnectionString);
        
        await Task.CompletedTask;
    }
    
    private HttpClient? _httpClient;
    internal HttpClient HttpClient
    {
        get
        {
            var client = _httpClient ??= _factory!.CreateClient();
            return client;
        }
    }

    // Called AFTER the host is built so we can resolve the singleton spy
    public HttpClient CreateClientAndBindSpy(bool createFreshClient = true)
    {
        if (createFreshClient)
        {
            _factory?.Dispose();
            _factory = new ApiFactory(_postgresConnectionString!);
        }

        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        EmailSpy = _factory!.Services.GetRequiredService<FakeVerificationEmailService>();
        return client;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();

        _httpClient?.Dispose();
    }
}
