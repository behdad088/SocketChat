using Microsoft.AspNetCore.Builder;

namespace Shared.Middlewares;

public static class RequestIdMiddlewares
{
    public static IApplicationBuilder UseTraceIdentifierHeader(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Request-Id"] = context.TraceIdentifier;
                return Task.CompletedTask;
            });

            await next();
        });
        
        return app;
    }
}