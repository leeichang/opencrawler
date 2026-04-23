using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public class NotebookLmService : INotebookLmService
{
    private static readonly string[] Scopes = { "https://www.googleapis.com/auth/cloud-platform" };

    private readonly IConfigService _cfg;
    private readonly HttpClient _http;
    private readonly ILogger<NotebookLmService> _log;

    private GoogleCredential? _credCache;
    private string? _cachedToken;
    private DateTime _tokenExpires = DateTime.MinValue;

    public NotebookLmService(IConfigService cfg, HttpClient http, ILogger<NotebookLmService> log)
    {
        _cfg = cfg;
        _http = http;
        _log = log;
    }

    private GcpConfig RequireGcp()
    {
        var g = _cfg.Current.Gcp ?? throw new InvalidOperationException("GCP not configured.");
        if (string.IsNullOrWhiteSpace(g.ServiceAccountJsonPath))
            throw new InvalidOperationException("Service Account JSON path required.");
        if (string.IsNullOrWhiteSpace(g.ProjectNumber))
            throw new InvalidOperationException("Project Number required.");
        return g;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpires.AddMinutes(-1))
            return _cachedToken;

        var gcp = RequireGcp();
        _credCache ??= (await GoogleCredential.FromFileAsync(gcp.ServiceAccountJsonPath!, ct))
            .CreateScoped(Scopes);
        _cachedToken = await _credCache.UnderlyingCredential.GetAccessTokenForRequestAsync(cancellationToken: ct);
        _tokenExpires = DateTime.UtcNow.AddMinutes(50);
        return _cachedToken!;
    }

    private string BaseUrl()
    {
        var gcp = RequireGcp();
        return $"https://{gcp.EndpointLocation}-discoveryengine.googleapis.com/v1alpha/" +
               $"projects/{gcp.ProjectNumber}/locations/{gcp.Location}";
    }

    private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var token = await GetAccessTokenAsync(ct);
        var req = new HttpRequestMessage(method, BaseUrl() + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    public async Task<string> CreateNotebookAsync(string title, CancellationToken ct = default)
    {
        var req = await NewRequestAsync(HttpMethod.Post, "/notebooks", ct);
        req.Content = JsonContent.Create(new { title });
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.GetProperty("notebookId").GetString()!;
    }

    public async Task<IReadOnlyList<string>> AddTextSourcesAsync(
        string notebookId,
        IReadOnlyList<TextSourceInput> sources,
        CancellationToken ct = default)
    {
        var req = await NewRequestAsync(HttpMethod.Post, $"/notebooks/{notebookId}/sources:batchCreate", ct);
        var body = new
        {
            userContents = sources.Select(s => new
            {
                textContent = new { sourceName = s.Name, content = s.Content }
            }).ToArray()
        };
        req.Content = JsonContent.Create(body);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var ids = new List<string>();
        if (doc.RootElement.TryGetProperty("sources", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("sourceId", out var id))
                    ids.Add(id.GetString()!);
            }
        }
        return ids;
    }

    public async Task<AudioOverviewStatus> StartAudioOverviewAsync(
        string notebookId,
        IReadOnlyList<string>? sourceIds,
        string episodeFocus,
        string languageCode,
        CancellationToken ct = default)
    {
        var req = await NewRequestAsync(HttpMethod.Post, $"/notebooks/{notebookId}/audioOverviews", ct);
        object body = sourceIds == null || sourceIds.Count == 0
            ? new { episodeFocus, languageCode }
            : new
            {
                sourceIds = sourceIds.Select(id => new { id }).ToArray(),
                episodeFocus,
                languageCode
            };
        req.Content = JsonContent.Create(body);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var state = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
        return new AudioOverviewStatus(state, null);
    }

    public async Task<AudioOverviewStatus> PollAudioOverviewAsync(
        string notebookId,
        CancellationToken ct = default)
    {
        var req = await NewRequestAsync(HttpMethod.Get, $"/notebooks/{notebookId}", ct);
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        string state = "UNKNOWN";
        string? audioUrl = null;
        if (doc.RootElement.TryGetProperty("audioOverview", out var ao))
        {
            if (ao.TryGetProperty("status", out var s)) state = s.GetString() ?? state;
            if (ao.TryGetProperty("audioUri", out var a)) audioUrl = a.GetString();
        }
        return new AudioOverviewStatus(state, audioUrl);
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var req = await NewRequestAsync(HttpMethod.Get, "/notebooks:listRecentlyViewed", ct);
            using var resp = await _http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "NotebookLM connection test failed");
            return false;
        }
    }
}
