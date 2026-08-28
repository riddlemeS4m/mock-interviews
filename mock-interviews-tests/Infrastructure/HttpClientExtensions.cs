using System.Text.RegularExpressions;

namespace MockInterviews.IntegrationTests.Infrastructure;

public static partial class HttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        this HttpClient client,
        string path,
        IEnumerable<KeyValuePair<string, string>> fields)
        => await client.PostFormWithAntiforgeryAsync(path, path, fields);

    public static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        this HttpClient client,
        string tokenPath,
        string path,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var page = await client.GetAsync(tokenPath);
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var token = AntiforgeryToken().Match(html).Groups["token"].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        return await client.PostAsync(path, new FormUrlEncodedContent(fields.Append(
            new KeyValuePair<string, string>("__RequestVerificationToken", token))));
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryToken();
}
