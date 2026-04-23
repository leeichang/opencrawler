namespace OpenCrawler.Core.Services.Fetchers;

public class FastHtmlFetcher : IHtmlFetcher
{
    private readonly HttpClient _http;

    public string ModeName => "fast";

    public FastHtmlFetcher(HttpClient http)
    {
        _http = http;
    }

    public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        req.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        req.Headers.AcceptLanguage.ParseAdd("zh-TW,zh;q=0.9,en;q=0.8");

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, ct);
        resp.EnsureSuccessStatusCode();

        var html = await resp.Content.ReadAsStringAsync(ct);
        var final = resp.RequestMessage?.RequestUri?.ToString() ?? url;

        var headers = new Dictionary<string, string>();
        foreach (var h in resp.Headers)
            headers[h.Key] = string.Join(", ", h.Value);
        foreach (var h in resp.Content.Headers)
            headers[h.Key] = string.Join(", ", h.Value);

        return new FetchResult(html, final, headers);
    }
}
