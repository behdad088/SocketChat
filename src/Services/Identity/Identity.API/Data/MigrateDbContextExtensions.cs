using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.Data;

internal static class MigrateDbContextExtensions
{
    private static readonly string ActivitySourceName = "DbMigrations";
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static IServiceCollection AddDbSeeder<TContext, TDbSeeder>(this IServiceCollection services)
        where TContext : DbContext
        where TDbSeeder : class, IDbSeeder<TContext>
    {
        return services.AddScoped<IDbSeeder<TContext>, TDbSeeder>();
    }

    // Called explicitly between builder.Build() and app.Run() so migration/seeding is
    // guaranteed complete before the host serves any request or resolves any other
    // startup-time singleton (e.g. Data Protection's key ring, which otherwise can race
    // ahead of this and find the schema not yet created).
    public static Task MigrateDatabaseAsync<TContext, TDbSeeder>(this IServiceProvider services)
        where TContext : DbContext
        where TDbSeeder : class, IDbSeeder<TContext>
        => services.MigrateDbContextAsync<TContext>((context, sp) =>
            sp.GetRequiredService<IDbSeeder<TContext>>().SeedAsync(context));

    private static async Task MigrateDbContextAsync<TContext>(
        this IServiceProvider services,
        Func<TContext, IServiceProvider, Task> seeder) where TContext : DbContext
    {
        using var scope = services.CreateScope();
        var scopeServices = scope.ServiceProvider;
        var logger = scopeServices.GetRequiredService<ILogger<TContext>>();
        var context = scopeServices.GetService<TContext>();

        using var activity = ActivitySource.StartActivity($"Migration operation {typeof(TContext).Name}");

        try
        {
            if (context == null)
            {
                logger.LogError("DbContext {DbContextName} is not registered in the service provider.", typeof(TContext).Name);
                return;
            }
            
            logger.LogInformation("Migrating database associated with context {DbContextName}", typeof(TContext).Name);

            var strategy = context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(() => InvokeSeeder(seeder, context, scopeServices));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database used on context {DbContextName}", typeof(TContext).Name);

            // activity.SetExceptionTags(ex);

            throw;
        }
    }

    private static async Task InvokeSeeder<TContext>(
        Func<TContext, IServiceProvider, Task> seeder,
        TContext context,
        IServiceProvider services)
        where TContext : DbContext
    {
        using var activity = ActivitySource.StartActivity($"Migrating {typeof(TContext).Name}");

        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        await seeder(context, services);
    }
}

public interface IDbSeeder<in TContext> where TContext : DbContext
{
    Task SeedAsync(TContext context);
}

