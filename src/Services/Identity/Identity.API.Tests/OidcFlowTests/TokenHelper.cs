using System.Text.Json.Nodes;

namespace Identity.API.Tests.OidcFlowTests;

public record TokenResponse(string? AccessToken, string? Error, bool IsError);

internal static class TokenHelper
{
    public static async Task<TokenResponse> RequestPasswordTokenAsync(
        HttpClient client,
        string username,
        string password,
        string scopes = "openid profile chat")
    {
        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "postman-client-password",
            ["username"] = username,
            ["password"] = password,
            ["scope"] = scopes
        };

        var response = await client.PostAsync("/connect/token",
            new FormUrlEncodedContent(body));

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(json);

        if (!response.IsSuccessStatusCode)
            return new TokenResponse(null, doc?["error"]?.GetValue<string>(), true);

        return new TokenResponse(doc?["access_token"]?.GetValue<string>(), null, false);
    }

    public static async Task<JsonNode?> IntrospectTokenAsync(
        HttpClient client,
        string token,
        string resourceName,
        string resourceSecret)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        var credentials = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{resourceName}:{resourceSecret}"));
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("token", token)
        ]);

        var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(json);
    }

    public static IEnumerable<string> ParsePermissions(string accessToken)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2) return [];

        var payload = parts[1];
        // Pad base64url
        payload = payload.Replace('-', '+').Replace('_', '/');
        var padding = (4 - payload.Length % 4) % 4;
        payload += new string('=', padding);

        var bytes = Convert.FromBase64String(payload);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var doc = JsonNode.Parse(json);

        var perms = doc?["permissions"];
        if (perms is JsonArray arr)
            return arr.Select(p => p?.GetValue<string>() ?? "").Where(s => s.Length > 0);
        if (perms is JsonValue val)
            return [val.GetValue<string>()];
        return [];
    }

    public static string? ParseClaim(string accessToken, string claimType)
    {
        var parts = accessToken.Split('.');
        if (parts.Length < 2) return null;
        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        var padding = (4 - payload.Length % 4) % 4;
        payload += new string('=', padding);
        var bytes = Convert.FromBase64String(payload);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        return JsonNode.Parse(json)?[claimType]?.GetValue<string>();
    }
}
