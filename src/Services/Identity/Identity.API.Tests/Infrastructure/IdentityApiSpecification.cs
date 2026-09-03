using Microsoft.AspNetCore.Mvc.Testing;

namespace Identity.API.Tests.Infrastructure;

public class IdentityApiSpecification : IAsyncLifetime
{
    private readonly WebApiContainerFactory _webApiContainer = new();
    public ApiFactory? _factory;
    private string? _postgresConnectionString;
    private string? _rabbitMqUri;
    public TestVerificationEmailService TestEmail { get; private set; } = null!;
    public string PostgresConnectionString => _postgresConnectionString!;
    public string RabbitMqAmqpUri => _webApiContainer.RabbitMqAmqpUri;

    public async Task InitializeAsync()
    {
        await _webApiContainer.InitializeAsync();
        _postgresConnectionString = _webApiContainer.PostgresConnectionString;
        _rabbitMqUri = _webApiContainer.RabbitMqUri;

        _factory = new ApiFactory(_postgresConnectionString, _rabbitMqUri);

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

    public HttpClient CreateClientAndBindSpy(bool createFreshClient = true)
    {
        if (createFreshClient)
        {
            _factory?.Dispose();
            _factory = new ApiFactory(_postgresConnectionString!, _rabbitMqUri!);
        }

        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            HandleCookies = true
        });

        TestEmail = _factory!.Services.GetRequiredService<TestVerificationEmailService>();
        return client;
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();

        _httpClient?.Dispose();
    }
}
