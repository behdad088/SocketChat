using Microsoft.Extensions.Options;
using Refit;

namespace Identity.API.ApiClients.Mailtrap;

public static class DependencyInjectionExtensions
{
    public static void AddMailTrapServicesApiClient(
        this IServiceCollection services,
        ConfigurationManager configuration
    )
    {
        services.Configure<MailTrapServicesSettings>(settings =>
        {
            settings.BaseUrl = configuration.GetValue<string>("EmailService:base_url");
            settings.Sender = configuration.GetValue<string>("EmailService:sender");
            settings.AuthorizationKey = configuration.GetValue<string>("EmailService:authentication_key");
        });

        services.AddRefitClient<IMailTrapServicesClient>()
            .ConfigureHttpClient((p, c) =>
            {
                var settings = p.GetRequiredService<IOptions<MailTrapServicesSettings>>();
                c.BaseAddress = new Uri(settings.Value.BaseUrl!);
            });
    }
}