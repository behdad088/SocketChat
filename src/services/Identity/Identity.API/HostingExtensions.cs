using System.Threading.RateLimiting;
using Duende.IdentityServer;
using Microsoft.AspNetCore.DataProtection;
using Duende.IdentityServer.Services;
using Identity.API.ApiClients.Mailtrap;
using Identity.API.Data;
using Identity.API.Models;
using Identity.API.Services;
using Identity.API.Services.EmailService;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Identity.API;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages(options =>
        {
            options.Conventions.AddPageRoute("/Account/EmailVerification/Index", "/Account/email-verification");
            options.Conventions.AddPageRoute("/Account/ResetPassword/Index", "/Account/reset-password");
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("login", o =>
            {
                o.PermitLimit = 10;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });
            options.AddFixedWindowLimiter("register", o =>
            {
                o.PermitLimit = 5;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });
            options.AddFixedWindowLimiter("forgot-password", o =>
            {
                o.PermitLimit = 5;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                o.QueueLimit = 0;
            });
        });
        builder.Services.AddMailTrapServicesApiClient(builder.Configuration);

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDB")));

        builder.Services.AddDataProtection()
            .SetApplicationName("identity-api")
            .PersistKeysToDbContext<ApplicationDbContext>();

        builder.Services.AddMigration<ApplicationDbContext, UsersSeed>();

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var identityServerBuilder = builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
                if (!string.IsNullOrEmpty(issuerUri))
                    options.IssuerUri = issuerUri;
            })
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryApiResources(Config.ApiResources)
            .AddInMemoryClients(Config.Clients)
            .AddAspNetIdentity<ApplicationUser>();

        // In development use an ephemeral in-memory signing key so no key material
        // is written to disk or committed to source control.
        // In production, replace this with a certificate loaded from a secrets manager
        // (e.g. Azure Key Vault) or configure Duende's automatic key management backed
        // by a database store via AddOperationalStore().
        if (builder.Environment.IsDevelopment())
            identityServerBuilder.AddDeveloperSigningCredential();

        builder.Services.AddTransient<IProfileService, ProfileService>();
        builder.Services.AddTransient<IVerificationEmailService, MailKitService>();

        builder.Services.AddAuthentication()
            .AddGoogle(options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;

                // register your IdentityServer with Google at https://console.developers.google.com
                // enable the Google+ API
                // set the redirect URI to https://localhost:5001/signin-google
                options.ClientId = "copy client ID from Google here";
                options.ClientSecret = "copy client secret from Google here";
            });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapRazorPages();

        return app;
    }
}
