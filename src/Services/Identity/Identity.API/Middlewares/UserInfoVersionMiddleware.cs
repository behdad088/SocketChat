namespace Identity.API.Middlewares;

// Duende's userinfo endpoint offers no hook for response headers, so the
// user version captured by ProfileService (via HttpContext.Items) is emitted
// here as an ETag. Must be registered before UseIdentityServer, which
// terminates the pipeline for /connect/userinfo.
public class UserInfoVersionMiddleware(RequestDelegate next)
{
    public const string VersionItemKey = "identity:userinfo-user-version";

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/connect/userinfo", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == StatusCodes.Status200OK &&
                    context.Items.TryGetValue(VersionItemKey, out var version))
                {
                    context.Response.Headers.ETag = $"\"{version}\"";
                }

                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
