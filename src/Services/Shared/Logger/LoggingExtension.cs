using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Exceptions;
using Serilog.Formatting.Compact;
using Serilog.Formatting.Json;
using Serilog.Sinks.Elasticsearch;

namespace Shared.Logger;


public static class LoggingExtension
{
    public static void SetupLogging(this WebApplicationBuilder builder,
        string appName,
        string environment,
        string? elasticSearchConnectionString,
        bool allowLoggingIntoFile = false,
        Dictionary<string, LogEventLevel>? minimumLevelOverrides = null)
    {
        var logger = CreateLogger(
            appName,
            environment,
            elasticSearchConnectionString,
            allowLoggingIntoFile,
            minimumLevelOverrides);

        builder.Host.UseSerilog(logger);
    }

    private static ILogger CreateLogger(
        string appName,
        string environment,
        string? elasticSearchConnectionString,
        bool allowLoggingIntoFile, 
        Dictionary<string, LogEventLevel>? minimumLevelOverrides)
    {
        var loggerConfiguration = CreateLoggerConfiguration(appName, environment, minimumLevelOverrides);

        if (allowLoggingIntoFile)
            loggerConfiguration.WriteTo.File(new CompactJsonFormatter(), $"log/{appName}_json_.log",
                rollingInterval: RollingInterval.Day);

        if (!string.IsNullOrWhiteSpace(elasticSearchConnectionString))
            loggerConfiguration.WriteTo.Elasticsearch(ConfigureElasticSink(elasticSearchConnectionString));

        Log.Logger = loggerConfiguration.CreateBootstrapLogger();
        return Log.Logger;
    }

    private static LoggerConfiguration CreateLoggerConfiguration(string appName, string environment,
        Dictionary<string, LogEventLevel>? minimumLevelOverrides)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.WithProperty("AppName", appName)
            .Enrich.WithProperty("Environment", environment)
            .Enrich.WithMachineName()
            .Enrich.FromLogContext()
            .Enrich.WithProcessId()
            .Enrich.WithExceptionDetails()
            .Enrich.WithSpan()
            .Enrich.FromLogContext()
            .WriteTo.Console(new JsonFormatter(renderMessage: true));

        if (minimumLevelOverrides != null && minimumLevelOverrides.Count != 0)
            foreach (var kv in minimumLevelOverrides)
                loggerConfiguration.MinimumLevel.Override(kv.Key, kv.Value);

        return loggerConfiguration;
    }
    
    private static ElasticsearchSinkOptions ConfigureElasticSink(string elasticSearchConnectionString)
    {
        return new ElasticsearchSinkOptions(new Uri(elasticSearchConnectionString))
        {
            AutoRegisterTemplate = true,
            AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
            MinimumLogEventLevel = LogEventLevel.Information,
        };
    }
}