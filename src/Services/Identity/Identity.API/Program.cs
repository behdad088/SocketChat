using Identity.API;
using Identity.API.Configurations.ConfigurationOptions;
using Serilog;
using Shared.Configurations;
using Shared.HealthChecks;
using Shared.Logger;
using Shared.Middlewares;
using Shared.OpenTelemetry;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.TrySetConfiguration<LoggerConfigurations>(builder.Configuration, out var loggerConfigurations);

    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")!;
    const string serviceName = "identity.api";
    builder.Services.AddOpenTelemetryOtl(serviceName);
    builder.SetupLogging("Identity Service", environment, loggerConfigurations.ElasticSearch);
    builder.Services.AddHealthChecks(builder.Configuration);

    var app = builder
        .ConfigureServices()
        .ConfigurePipeline();
    app.UseSerilogRequestLogging(options =>
    {
        options.IncludeQueryInRequestPath = true;
        options.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });
    app.UseTraceIdentifierHeader();
    app.MapDefaultHealthChecks();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}

public partial class Program
{
}
