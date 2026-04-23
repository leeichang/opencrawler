using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OpenCrawler.Core.Services;

public class GeminiSummaryService : IGeminiSummaryService
{
    private readonly IConfigService _cfg;
    private readonly HttpClient _http;
    private readonly ILogger<GeminiSummaryService> _log;

    public GeminiSummaryService(IConfigService cfg, HttpClient http, ILogger<GeminiSummaryService> log)
    {
        _cfg = cfg;
        _http = http;
        _log = log;
    }

    public async Task<string> SummarizeAsync(string prompt, string articleText, CancellationToken ct = default)
    {
        var gcp = _cfg.Current.Gcp ?? throw new InvalidOperationException("GCP config missing");
        if (string.IsNullOrWhiteSpace(gcp.GeminiApiKey))
            throw new InvalidOperationException("Gemini API Key required");

        var model = string.IsNullOrWhiteSpace(gcp.GeminiModel) ? "gemini-2.5-pro" : gcp.GeminiModel;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={gcp.GeminiApiKey}";

        var body = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"{prompt}\n\n---\n\n{articleText}" } } }
            },
            generationConfig = new { temperature = 0.3, maxOutputTokens = 8192 }
        };

        using var resp = await _http.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        return text ?? "";
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await SummarizeAsync("回覆 OK", "", ct);
            return !string.IsNullOrEmpty(result);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gemini connection test failed");
            return false;
        }
    }
}
