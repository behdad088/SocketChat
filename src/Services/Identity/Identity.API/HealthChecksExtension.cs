using Shared.HealthChecks;

namespace Identity.API;

public static class HealthChecksExtension
{
    public static IServiceCollection AddHealthChecks(this IServiceCollection services,
        ConfigurationManager configuration)
    {
        services.AddDefaultHealthChecks();
        var databaseConfiguration = configuration.GetConnectionString("IdentityDB")!;

        services.AddHealthChecks()
            .AddNpgSql(databaseConfiguration, name: "postgres", tags: ["ready", "liveness"]);

        return services;
    }
}
