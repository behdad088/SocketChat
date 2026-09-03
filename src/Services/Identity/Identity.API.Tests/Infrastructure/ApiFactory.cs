using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.API.Tests.Infrastructure;

public class ApiFactory(string postgresConnection, string rabbitMqUri) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDB", postgresConnection);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        Environment.SetEnvironmentVariable("EmailService__base_url", "http://localhost:9999");
        Environment.SetEnvironmentVariable("EmailService__sender", "test@test.com");
        Environment.SetEnvironmentVariable("EmailService__authentication_key", "fake-key");
        Environment.SetEnvironmentVariable("service_url", "http://localhost");
        Environment.SetEnvironmentVariable("web_url", "http://localhost");

        Environment.SetEnvironmentVariable("RabbitMQ__Uri", rabbitMqUri);
        Environment.SetEnvironmentVariable("RabbitMQ__Username", "rabbitmq");
        Environment.SetEnvironmentVariable("RabbitMQ__Password", "rabbitmq");
        Environment.SetEnvironmentVariable("Outbox__PollIntervalSeconds", "1");
        Environment.SetEnvironmentVariable("Outbox__PublishTimeoutSeconds", "2");

        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVerificationEmailService>();
            services.AddSingleton<TestVerificationEmailService>();
            services.AddSingleton<IVerificationEmailService>(
                sp => sp.GetRequiredService<TestVerificationEmailService>());
        });
    }
}
