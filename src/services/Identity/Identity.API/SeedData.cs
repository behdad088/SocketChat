using System.Security.Claims;
using Duende.IdentityModel;
using Identity.API.Data;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Serilog;

namespace Identity.API;

public class UsersSeed(
    ILogger<UsersSeed> logger,
    UserManager<ApplicationUser> userManager, 
    RoleManager<IdentityRole> roleManager) : IDbSeeder<ApplicationDbContext>
{
    public async Task SeedAsync(ApplicationDbContext context)
    {
        await AddRoles();
        var alice = await userManager.FindByNameAsync("alice");

        if (alice == null)
        {
            alice = new ApplicationUser
            {
                UserName = "AliceSmith@email.com",
                Email = "AliceSmith@email.com",
                EmailConfirmed = true,
                Id = Guid.NewGuid().ToString(),
                LastName = "Smith",
                Name = "Alice",
                PhoneNumber = "0724423456"
            };

            var result = userManager.CreateAsync(alice, "Pass123$").Result;

            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            result = userManager.AddToRoleAsync(alice, Config.Roles.Customer).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            
            result = AddClaims(alice, [
                new Claim(JwtClaimTypes.Name, "Alice Smith"),
                new Claim(JwtClaimTypes.GivenName, "Alice"),
                new Claim(JwtClaimTypes.FamilyName, "Smith"),
                new Claim(JwtClaimTypes.WebSite, "http://alice.example.com")
            ]);
            
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("alice created");
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("alice already exists");
            }
        }

        var bob = await userManager.FindByNameAsync("bob");

        if (bob == null)
        {
            bob = new ApplicationUser
            {
                UserName = "BobSmith@email.com",
                Email = "BobSmith@email.com",
                EmailConfirmed = true,
                Id = Guid.NewGuid().ToString(),
                LastName = "Smith",
                Name = "Bob",
                PhoneNumber = "0724456789"
            };

            var result = await userManager.CreateAsync(bob, "Pass123$");

            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            
            result = userManager.AddToRoleAsync(bob, Config.Roles.Admin).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            
            result = AddClaims(bob, [
                new(JwtClaimTypes.Name, "Bob Smith"),
                new Claim(JwtClaimTypes.GivenName, "Bob"),
                new Claim(JwtClaimTypes.FamilyName, "Smith"),
                new Claim(JwtClaimTypes.WebSite, "http://bob.example.com"),
                new Claim("location", "somewhere")
            ]);
            
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("bob created");
            }
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("bob already exists");
            }
        }
    }
    private IdentityResult AddClaims(
        ApplicationUser user,
        Claim[] claims)
    {
        return userManager.AddClaimsAsync(user, claims).Result;
    }
    
    private async Task AddRoles()
    {
        
        if (!roleManager.RoleExistsAsync(Config.Roles.Admin).Result)
        {
            var role = new IdentityRole(Config.Roles.Admin);
            var result = roleManager.CreateAsync(role).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            Log.Debug("admin role created");
        }
        else
        {
            Log.Debug("admin role already exists");
        }
            
        if (!roleManager.RoleExistsAsync(Config.Roles.Customer).Result)
        {
            var role = new IdentityRole(Config.Roles.Customer);
            var result = roleManager.CreateAsync(role).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }
            Log.Debug("user role created");
        }
        else
        {
            Log.Debug("user role already exists");
        }
        
        foreach (var (policy, roles) in Config.RolePolicyDefinitions.PolicyToRoles)
        {
            foreach (var role in roles)
            {
                var availableRole = await roleManager.FindByNameAsync(role);
                if (availableRole != null)
                {
                    await roleManager.AddClaimAsync(availableRole,
                        new Claim("permissions", policy));
                }
            }
        }
    }
}