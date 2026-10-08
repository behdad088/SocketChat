using Chat.EventProcessor.Messaging;
using Chat.Storage;
using FluentValidation;
using Serilog;
using Shared.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddValidatorsFromAssemblies(AppDomain.CurrentDomain.GetAssemblies());

var chatDbConnectionString = builder.Configuration.GetConnectionString("ChatDB")
                             ?? throw new InvalidOperationException("Connection string 'ChatDB' is missing.");

builder.Services.AddChatStorage(chatDbConnectionString);
builder.Services.AddIdentityEventConsumers(builder.Configuration);
builder.Services.AddDefaultHealthChecks()
    .AddNpgSql(chatDbConnectionString, name: "postgres", tags: ["ready", "liveness"]);

var app = builder.Build();
app.MapDefaultHealthChecks();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();

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


namespace Chat.EventProcessor
{
    public class Program
    {
    }
}
