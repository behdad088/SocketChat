using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.API.Tests.Infrastructure;

public class ApiFactory(string postgresConnection) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__IdentityDB", postgresConnection);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        // Dummy values so the Refit MailTrap client resolves without throwing on BaseAddress
        Environment.SetEnvironmentVariable("EmailService__base_url", "http://localhost:9999");
        Environment.SetEnvironmentVariable("EmailService__sender", "test@test.com");
        Environment.SetEnvironmentVariable("EmailService__authentication_key", "fake-key");
        // Used by VerificationEmailService.EmailVerificationLink – not needed since we use the fake,
        // but set it so the real service doesn't throw if resolved accidentally.
        Environment.SetEnvironmentVariable("service_url", "http://localhost");

        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Replace the real VerificationEmailService with a spy that captures plaintext codes
            services.RemoveAll<IVerificationEmailService>();
            services.AddSingleton<FakeVerificationEmailService>();
            services.AddSingleton<IVerificationEmailService>(
                sp => sp.GetRequiredService<FakeVerificationEmailService>());
        });
    }
}