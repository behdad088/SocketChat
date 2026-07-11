using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Identity.API.Middlewares;
using Identity.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Identity.API.Services;

public class ProfileService : IProfileService
{
    public ProfileService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));
        var subjectId = subject.GetSubjectId() ?? string.Empty;
        // Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? string.Empty;

        var user = await _userManager.FindByIdAsync(subjectId);
        if (user == null)
            throw new ArgumentException("Invalid subject identifier");
        var userClaims = GetClaimsFromUser(user);

        if (context.Caller == IdentityServerConstants.ProfileDataCallers.UserInfoEndpoint)
        {
            // Issued live per userinfo request only; never into id/access
            // tokens, where the value would go stale for the token's lifetime.
            userClaims = userClaims.Append(new Claim("version", user.Version.ToString()));

            if (_httpContextAccessor.HttpContext is { } httpContext)
            {
                httpContext.Items[UserInfoVersionMiddleware.VersionItemKey] = user.Version;
            }
        }

        IEnumerable<Claim> roleClaims = [];
        
        if (_userManager.SupportsUserRole)
        {
            var roles = await _userManager.GetRolesAsync(user);
            roleClaims = await GetClaimFromRole(roles.ToList());
        }
        context.IssuedClaims = userClaims.Concat(roleClaims).ToList();
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));

        var subjectId = subject.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? string.Empty;
        var user = await _userManager.FindByIdAsync(subjectId);

        context.IsActive = false;

        if (user != null)
        {
            if (_userManager.SupportsUserSecurityStamp)
            {
                var securityStamp = subject.Claims.Where(c => c.Type == "security_stamp").Select(c => c.Value).SingleOrDefault();
                if (securityStamp != null)
                {
                    var dbSecurityStamp = await _userManager.GetSecurityStampAsync(user);
                    if (dbSecurityStamp != securityStamp)
                        return;
                }
            }

            context.IsActive =
                !user.LockoutEnabled ||
                !user.LockoutEnd.HasValue ||
                user.LockoutEnd <= DateTime.UtcNow;
        }
    }

    private async Task<IEnumerable<Claim>> GetClaimFromRole(List<string> roles)
    {
        var claims = new List<Claim>();
        
        foreach (var roleName in roles)
        {
            claims.Add(new Claim(JwtClaimTypes.Role, roleName));

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role != null)
            {
                var roleClaims = await _roleManager.GetClaimsAsync(role);

                // Only include "permission" claims, if desired
                var permissionClaims = roleClaims
                    .Where(c => c.Type == "permissions");

                claims.AddRange(permissionClaims);
            }
        }
        
        // Ensure distinct claims to avoid duplicates (some roles might have the same claims)
        return claims.Distinct();
    }
    
    private IEnumerable<Claim> GetClaimsFromUser(ApplicationUser user)
    {
            var claims = new List<Claim>
            {
                new Claim(JwtClaimTypes.Subject, user.Id)
            };

            if (user.UserName != null)
            {
                claims.Add(new Claim(JwtClaimTypes.PreferredUserName, user.UserName));
                claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName));
            }
            if (!string.IsNullOrWhiteSpace(user.Name))
                claims.Add(new Claim("name", user.Name));

            if (!string.IsNullOrWhiteSpace(user.LastName))
                claims.Add(new Claim("last_name", user.LastName));

            if (!string.IsNullOrWhiteSpace(user.ProfilePicture))
                claims.Add(new Claim("profile_picture", user.ProfilePicture));
            

            if (_userManager.SupportsUserEmail)
            {
                claims.AddRange([
                    new Claim(JwtClaimTypes.EmailVerified, user.EmailConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                ]);

                if (user.Email != null)
                {
                    claims.Add(new Claim(JwtClaimTypes.Email, user.Email));
                }
            }

            if (_userManager.SupportsUserPhoneNumber && !string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                claims.AddRange([
                    new Claim(JwtClaimTypes.PhoneNumber, user.PhoneNumber),
                    new Claim(JwtClaimTypes.PhoneNumberVerified, user.PhoneNumberConfirmed ? "true" : "false", ClaimValueTypes.Boolean)
                ]);
            }

            return claims;
    }
}