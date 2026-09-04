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

    public static async Task<HttpResponseMessage> PostCsvWithAntiforgeryAsync(
        this HttpClient client,
        string path,
        string csv)
    {
        var page = await client.GetAsync(path);
        page.EnsureSuccessStatusCode();
        var token = AntiforgeryToken().Match(await page.Content.ReadAsStringAsync()).Groups["token"].Value;
        Assert.False(string.IsNullOrWhiteSpace(token));

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(token), "__RequestVerificationToken");
        content.Add(new StringContent(csv), "RosterData", "roster.csv");
        return await client.PostAsync(path, content);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryToken();
}
