using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Identity.API.Tests.Infrastructure;

/// <summary>
/// Extracts CSRF tokens from Razor Pages responses for use in POST requests.
/// </summary>
public static class AntiForgeryHelper
{
    private static readonly Regex TokenPattern =
        new(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<string> GetTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = TokenPattern.Match(html);
        if (!match.Success)
            throw new InvalidOperationException($"No __RequestVerificationToken found on {url}");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string url,
        Dictionary<string, string> formData)
    {
        var token = await GetTokenAsync(client, url);
        formData["__RequestVerificationToken"] = token;
        var content = new FormUrlEncodedContent(formData);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        return await client.PostAsync(url, content);
    }
}
