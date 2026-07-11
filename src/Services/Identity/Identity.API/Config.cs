using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Identity.API;

public static class Config
{
    private static class Policies
    {
        public const string UserCanReadMessage = "chat:user-message:read";
        public const string UserCanWriteMessage = "chat:user-message:write";
    }

    public static class RolePolicyDefinitions
    {
        public static readonly Dictionary<string, string[]> PolicyToRoles = new()
        {
            { Policies.UserCanReadMessage, [Roles.Admin, Roles.Customer] },
            { Policies.UserCanWriteMessage, [Roles.Admin, Roles.Customer] }
        };
    }

    public static class Roles
    {
        public const string Admin = "admin";
        public const string Customer = "customer";
    }

    private static class ScopeNames
    {
        public const string Chat = "chat";
    }

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile()
    ];

    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new(ScopeNames.Chat, "Chat Service"),
        new(IdentityServerConstants.LocalApi.ScopeName, "Identity API")
    ];

    public static IEnumerable<ApiResource> ApiResources =>
    [
        new("chat", "Chat API")
        {
            Scopes = { ScopeNames.Chat },
            // Secret used by the Basket service to authenticate at /connect/introspect
            ApiSecrets = { new Secret("chat-secret".Sha256()) }
        }
    ];

    public static IEnumerable<Client> Clients =>
    [
        new Client
        {
            ClientId = "postman-client",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "https://oauth.pstmn.io/v1/browser-callback" },
            PostLogoutRedirectUris = { "https://oauth.pstmn.io/v1/browser-callback" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                ScopeNames.Chat,
                IdentityServerConstants.LocalApi.ScopeName
            },
            AllowAccessTokensViaBrowser = true,
            AllowOfflineAccess = true
        },

        new Client
        {
            ClientId = "postman-client-password",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPasswordAndClientCredentials,
            RequirePkce = false,
            RequireClientSecret = false,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                ScopeNames.Chat,
                IdentityServerConstants.LocalApi.ScopeName
            },
            AllowOfflineAccess = true
        },
        new Client
        {
            ClientId = "device-client",
            AllowedGrantTypes = GrantTypes.DeviceFlow,
            RequireClientSecret = false,
            AllowOfflineAccess = true,
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                ScopeNames.Chat,
                IdentityServerConstants.LocalApi.ScopeName
            }
        }
    ];
}