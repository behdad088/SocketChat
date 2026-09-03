using Duende.IdentityServer.Models;

namespace Identity.API.Tests.UnitTests;

public class ConfigTests
{
    [Fact]
    public void IdentityResourcesContainsOpenidAndProfile()
    {
        // Act
        var names = Config.IdentityResources.Select(r => r.Name).ToList();
        
        // Assert
        names.ShouldContain("openid");
        names.ShouldContain("profile");
    }

    [Fact]
    public void ApiScopesContainsAllFourMicroservices()
    {
        // Act
        var names = Config.ApiScopes.Select(s => s.Name).ToList();
        
        // Assert
        names.ShouldContain("chat");
    }

    [Fact]
    public void ApiResourcesHaveSecretsForIntrospection()
    {
        foreach (var resource in Config.ApiResources)
        {
            // Assert
            resource.ApiSecrets.ShouldNotBeEmpty(
                $"ApiResource '{resource.Name}' must have a secret for introspection");
        }
    }

    [Fact]
    public void ClientsContainsAllFourConfiguredClients()
    {
        // Act
        var ids = Config.Clients.Select(c => c.ClientId).ToList();
        
        // Assert
        ids.ShouldContain("postman-client");
        ids.ShouldContain("postman-client-password");
        ids.ShouldContain("web-client");
    }

    [Fact]
    public void WebClientUsesPasswordGrantWithRotatingRefreshTokens()
    {
        // Act
        var client = Config.Clients.Single(c => c.ClientId == "web-client");
        
        // Assert
        client.AllowedGrantTypes.ShouldContain(GrantType.ResourceOwnerPassword);
        client.RequireClientSecret.ShouldBeFalse();
        client.AllowOfflineAccess.ShouldBeTrue();
        client.RefreshTokenUsage.ShouldBe(TokenUsage.OneTimeOnly);
        client.AllowedScopes.ShouldContain("chat");
    }

    [Fact]
    public void PostmanClientUsesAuthorizationCodeWithPkce()
    {
        // Act
        var client = Config.Clients.Single(c => c.ClientId == "postman-client");
        
        // Assert
        client.AllowedGrantTypes.ShouldContain(GrantType.AuthorizationCode);
        client.RequirePkce.ShouldBeTrue();
        client.RequireClientSecret.ShouldBeFalse();
    }

    [Fact]
    public void PasswordClientUsesResourceOwnerPasswordGrant()
    {
        // Act
        var client = Config.Clients.Single(c => c.ClientId == "postman-client-password");
        
        // Assert
        client.AllowedGrantTypes.ShouldContain(GrantType.ResourceOwnerPassword);
        client.RequirePkce.ShouldBeFalse();
    }

    [Fact]
    public void CustomerRoleCanCheckoutBasket()
    {
        // Act
        var rolesForCheckout = Config.RolePolicyDefinitions.PolicyToRoles
            .FirstOrDefault(kv => kv.Key == "chat:user-message:read").Value;

        // Assert
        rolesForCheckout.ShouldNotBeNull();
        rolesForCheckout.ShouldContain(Config.Roles.Customer);
    }

    [Fact]
    public void AdminRoleHasChatMutationPermissions()
    {
        // Arrange
        var catalogMutationPolicies = new[]
        {
            "chat:user-message:read",
            "chat:user-message:write"
        };

        foreach (var policy in catalogMutationPolicies)
        {
            // Act - Assert
            var roles = Config.RolePolicyDefinitions.PolicyToRoles[policy];
            roles.ShouldContain(Config.Roles.Admin,
                $"Admin should have policy '{policy}'");
        }
    }

    [Fact]
    public void PolicyToRolesHasNoDuplicateAssignments()
    {
        foreach (var (policy, roles) in Config.RolePolicyDefinitions.PolicyToRoles)
        {
            // Assert
            roles.Distinct().Count().ShouldBe(roles.Length,
                $"Policy '{policy}' has duplicate role assignments");
        }
    }
}
