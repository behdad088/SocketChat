using Serilog;
using Shared.Exceptions.Handler;
using Shared.HealthChecks;
using Shared.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDefaultHealthChecks();
builder.Services.AddFastEndpoints();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
}

app.UseTraceIdentifierHeader();
app.MapDefaultHealthChecks();
app.UseHttpsRedirection();
app.UseProblemDetailsResponseExceptionHandler();

app.UseFastEndpoints(c =>
{
    c.Versioning.Prefix = "v";
    c.Versioning.PrependToRoute = true;
    c.Versioning.DefaultVersion = 1;
    c.Serializer.Options.PropertyNamingPolicy = null;
    c.Serializer.Options.AllowOutOfOrderMetadataProperties = true;
});

try
{
    await app.RunAsync();
}
catch (Exception e)
{
    Log.Fatal(e,"Unhandled Exception");
}
finally
{
    Log.Information("Log Complete");
    Log.CloseAndFlush();
}

namespace Chat.Api
{
    public class Program
    {
    }
}
