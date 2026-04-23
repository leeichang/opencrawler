using System.Text;
using System.Text.Json;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Models;
using OpenCrawler.Core.Services.Fetchers;

namespace OpenCrawler.Core.Services;

public class WebDownloader : IWebDownloader
{
    private readonly FastHtmlFetcher _fast;
    private readonly BrowserHtmlFetcher _browser;
    private readonly ChallengeDetector _detector;
    private readonly ReadabilityExtractor _readability;
    private readonly HttpClient _http;
    private readonly ILogger<WebDownloader> _log;

    private static readonly JsonSerializerOptions MetaJsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public WebDownloader(
        FastHtmlFetcher fast,
        BrowserHtmlFetcher browser,
        ChallengeDetector detector,
        ReadabilityExtractor readability,
        HttpClient http,
        ILogger<WebDownloader> log)
    {
        _fast = fast;
        _browser = browser;
        _detector = detector;
        _readability = readability;
        _http = http;
        _log = log;
    }

    public async Task<DownloadResult> DownloadAsync(
        string url,
        string targetFolder,
        FetchMode mode,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new DownloadProgress(DownloadStage.Starting, 0, 0, url));

        progress?.Report(new DownloadProgress(DownloadStage.FetchingHtml, 0, 0, url));
        var fetch = await FetchHtmlAsync(url, mode, ct);
        _log.LogInformation("Fetched {Url} via {Mode}, html {Len} bytes", url, fetch.mode, fetch.result.Html.Length);

        progress?.Report(new DownloadProgress(DownloadStage.ParsingHtml, 0, 0, null));
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(fetch.result.Html);

        var originalTitle = doc.Title?.Trim();
        var title = ExtractBestTitle(doc, originalTitle);

        Directory.CreateDirectory(targetFolder);
        var assetsDir = Path.Combine(targetFolder, "assets");
        Directory.CreateDirectory(assetsDir);

        StripScripts(doc);

        var assetUrls = CollectAssetUrls(doc, fetch.result.FinalUrl);
        var assetMap = new Dictionary<string, string>();
        var totalAssets = assetUrls.Count;
        var downloadedCount = 0;
        long totalBytes = 0;

        foreach (var assetUrl in assetUrls)
        {
            downloadedCount++;
            progress?.Report(new DownloadProgress(DownloadStage.DownloadingAssets, downloadedCount, totalAssets, assetUrl));
            try
            {
                var localName = await DownloadAssetAsync(assetUrl, assetsDir, ct);
                if (localName != null)
                {
                    assetMap[assetUrl] = $"assets/{localName}";
                    totalBytes += new FileInfo(Path.Combine(assetsDir, localName)).Length;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Asset download failed: {Url}", assetUrl);
            }
        }

        RewriteResourceUrls(doc, fetch.result.FinalUrl, assetMap);

        progress?.Report(new DownloadProgress(DownloadStage.Writing, 0, 0, null));
        InjectOfflineVisibilityPatch(doc);
        var indexPath = Path.Combine(targetFolder, "index.html");
        await File.WriteAllTextAsync(indexPath, doc.DocumentElement.OuterHtml, Encoding.UTF8, ct);

        var plain = _readability.ExtractPlainText(doc);
        await File.WriteAllTextAsync(Path.Combine(targetFolder, "content.txt"), plain, Encoding.UTF8, ct);

        var meta = new
        {
            sourceUrl = url,
            finalUrl = fetch.result.FinalUrl,
            title,
            originalTitle,
            fetchMode = fetch.mode,
            downloadedAt = DateTime.UtcNow,
            imageCount = assetMap.Count,
            sizeBytes = totalBytes
        };
        await File.WriteAllTextAsync(
            Path.Combine(targetFolder, "meta.json"),
            JsonSerializer.Serialize(meta, MetaJsonOpts),
            Encoding.UTF8, ct);

        var folderName = new DirectoryInfo(targetFolder).Name;
        var result = new DownloadResult(
            title,
            originalTitle,
            folderName,
            indexPath,
            fetch.result.FinalUrl,
            fetch.mode,
            assetMap.Count,
            totalBytes + new FileInfo(indexPath).Length);

        progress?.Report(new DownloadProgress(DownloadStage.Completed, 0, 0, null));
        return result;
    }

    private async Task<(FetchResult result, string mode)> FetchHtmlAsync(string url, FetchMode mode, CancellationToken ct)
    {
        if (mode == FetchMode.Fast)
            return (await _fast.FetchAsync(url, ct), "fast");
        if (mode == FetchMode.Browser)
            return (await _browser.FetchAsync(url, ct), "browser");

        try
        {
            var fastResult = await _fast.FetchAsync(url, ct);
            if (!_detector.LooksLikeChallenge(fastResult))
                return (fastResult, "fast");
            _log.LogInformation("Fast fetch looked like challenge; falling back to browser");
        }
        catch (Exception ex) when (_detector.IsAntiBotError(ex))
        {
            _log.LogInformation(ex, "Fast fetch blocked ({Status}); falling back to browser",
                (ex as HttpRequestException)?.StatusCode);
        }

        return (await _browser.FetchAsync(url, ct), "browser");
    }

    private const string OfflineCssPatch = @"
/* openCrawler offline visibility patch */
html, body { display: block !important; visibility: visible !important; opacity: 1 !important; }
#js_content, .rich_media_content, .rich_media_wrp, .rich_media_inner,
main, article, .article, .post, .entry-content, .post-content,
[role='main'], [data-role='article'] {
  visibility: visible !important;
  opacity: 1 !important;
  display: block !important;
  height: auto !important;
  max-height: none !important;
  overflow: visible !important;
}
[style*='display:none']:not(script):not(style) { }
img { max-width: 100%; height: auto; }
";

    private static string ExtractBestTitle(IHtmlDocument doc, string? docTitle)
    {
        // 優先用 <title>,但很多 SPA 的 <title> 是空 / 由 JS 填 / 只是站名,所以加 fallback
        if (LooksLikeGoodTitle(docTitle)) return docTitle!.Trim();

        // og:title / twitter:title
        foreach (var selector in new[] {
            "meta[property='og:title']",
            "meta[name='og:title']",
            "meta[name='twitter:title']",
            "meta[property='twitter:title']" })
        {
            var m = doc.QuerySelector(selector)?.GetAttribute("content")?.Trim();
            if (LooksLikeGoodTitle(m)) return m!;
        }

        // WeChat 特定 meta
        var wx = doc.QuerySelector("meta[name='description']")?.GetAttribute("content")?.Trim();
        // 不用 description,太長

        // 正文 h1 / h2(常見於內容區)
        foreach (var selector in new[] {
            "article h1", "main h1", "#js_content h1",
            ".rich_media_title", "h1#activity-name",
            "h1" })
        {
            var el = doc.QuerySelector(selector);
            var t = el?.TextContent?.Trim();
            if (LooksLikeGoodTitle(t)) return t!;
        }

        // 最後再吐一次 <title>(即使短也比 "untitled" 好)
        if (!string.IsNullOrWhiteSpace(docTitle)) return docTitle.Trim();
        return "untitled";
    }

    private static bool LooksLikeGoodTitle(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return false;
        t = t.Trim();
        if (t.Length < 4) return false;
        // 排除常見「站名 only」
        var lower = t.ToLowerInvariant();
        string[] junk = { "untitled", "document", "loading...", "loading…", "wechat" };
        if (junk.Contains(lower)) return false;
        return true;
    }

    private static void InjectOfflineVisibilityPatch(IHtmlDocument doc)
    {
        var head = doc.Head;
        if (head == null) return;
        var style = doc.CreateElement("style");
        style.SetAttribute("data-opencrawler", "offline-patch");
        style.TextContent = OfflineCssPatch;
        head.AppendChild(style);

        // 強制 un-hide 這些常見容器上的 inline style
        foreach (var sel in new[] { "#js_content", ".rich_media_content", "body" })
        {
            var el = doc.QuerySelector(sel);
            if (el != null)
            {
                el.RemoveAttribute("hidden");
                var inline = el.GetAttribute("style") ?? "";
                if (inline.Contains("display:none", StringComparison.OrdinalIgnoreCase) ||
                    inline.Contains("visibility:hidden", StringComparison.OrdinalIgnoreCase))
                {
                    el.SetAttribute("style", inline + ";display:block !important;visibility:visible !important;");
                }
            }
        }
    }

    private static void StripScripts(IHtmlDocument doc)
    {
        foreach (var s in doc.QuerySelectorAll("script").ToList())
            s.Remove();

        foreach (var el in doc.QuerySelectorAll("*").ToList())
        {
            var attrs = el.Attributes.Where(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var a in attrs) el.RemoveAttribute(a.Name);

            var href = el.GetAttribute("href");
            if (!string.IsNullOrEmpty(href) && href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                el.RemoveAttribute("href");
        }
    }

    private static readonly string[] LazyImgAttrs =
    {
        "data-src", "data-original", "data-lazy-src", "data-actualsrc", "data-echo"
    };

    private static List<string> CollectAssetUrls(IHtmlDocument doc, string baseUrl)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            if (!TryResolve(baseUrl, raw, out var abs)) return;
            if (seen.Add(abs)) list.Add(abs);
        }

        // Unwrap <noscript> — some sites put real img there for non-JS browsers
        foreach (var ns in doc.QuerySelectorAll("noscript").ToList())
        {
            var html = ns.InnerHtml;
            ns.InnerHtml = html;
        }

        foreach (var img in doc.QuerySelectorAll("img"))
        {
            Add(img.GetAttribute("src"));
            ParseSrcset(img.GetAttribute("srcset")).ForEach(Add);
            foreach (var a in LazyImgAttrs)
                Add(img.GetAttribute(a));
        }
        foreach (var src in doc.QuerySelectorAll("source"))
            ParseSrcset(src.GetAttribute("srcset")).ForEach(Add);
        foreach (var link in doc.QuerySelectorAll("link[rel='stylesheet']"))
            Add(link.GetAttribute("href"));

        return list;
    }

    private static List<string> ParseSrcset(string? srcset)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(srcset)) return result;
        foreach (var part in srcset.Split(','))
        {
            var trimmed = part.Trim();
            var spaceIdx = trimmed.IndexOf(' ');
            result.Add(spaceIdx < 0 ? trimmed : trimmed[..spaceIdx]);
        }
        return result;
    }

    private static bool TryResolve(string baseUrl, string raw, out string absolute)
    {
        absolute = "";
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return false;
        if (raw.StartsWith("#") || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            var uri = new Uri(new Uri(baseUrl), raw);
            if (uri.Scheme != "http" && uri.Scheme != "https") return false;
            absolute = uri.ToString();
            return true;
        }
        catch { return false; }
    }

    private async Task<string?> DownloadAssetAsync(string url, string assetsDir, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0");
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var ct2 = resp.Content.Headers.ContentType?.MediaType;
        var fileName = FileNameSanitizer.GenerateAssetFileName(url, ct2);
        var path = Path.Combine(assetsDir, fileName);

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        await using var fs = File.Create(path);
        await stream.CopyToAsync(fs, ct);
        return fileName;
    }

    private static void RewriteResourceUrls(IHtmlDocument doc, string baseUrl, IReadOnlyDictionary<string, string> map)
    {
        foreach (var img in doc.QuerySelectorAll("img"))
        {
            // 優先用 data-src 等延遲載入屬性覆寫 src,讓本地開啟時就能看到圖片
            foreach (var a in LazyImgAttrs)
            {
                var lazy = img.GetAttribute(a);
                if (!string.IsNullOrWhiteSpace(lazy) && TryResolve(baseUrl, lazy, out var abs) && map.TryGetValue(abs, out var local))
                {
                    img.SetAttribute("src", local);
                    img.RemoveAttribute(a);
                    break;
                }
            }
            RewriteAttr(img, "src", baseUrl, map);
            var srcset = img.GetAttribute("srcset");
            if (!string.IsNullOrWhiteSpace(srcset))
                img.SetAttribute("srcset", RewriteSrcset(srcset, baseUrl, map));
        }
        foreach (var src in doc.QuerySelectorAll("source"))
        {
            var srcset = src.GetAttribute("srcset");
            if (!string.IsNullOrWhiteSpace(srcset))
                src.SetAttribute("srcset", RewriteSrcset(srcset, baseUrl, map));
        }
        foreach (var link in doc.QuerySelectorAll("link[rel='stylesheet']"))
            RewriteAttr(link, "href", baseUrl, map);
    }

    private static void RewriteAttr(IElement el, string attr, string baseUrl, IReadOnlyDictionary<string, string> map)
    {
        var raw = el.GetAttribute(attr);
        if (string.IsNullOrWhiteSpace(raw)) return;
        if (!TryResolve(baseUrl, raw, out var abs)) return;
        if (map.TryGetValue(abs, out var local)) el.SetAttribute(attr, local);
    }

    private static string RewriteSrcset(string srcset, string baseUrl, IReadOnlyDictionary<string, string> map)
    {
        var parts = new List<string>();
        foreach (var part in srcset.Split(','))
        {
            var trimmed = part.Trim();
            var spaceIdx = trimmed.IndexOf(' ');
            var rawUrl = spaceIdx < 0 ? trimmed : trimmed[..spaceIdx];
            var rest = spaceIdx < 0 ? "" : trimmed[spaceIdx..];
            if (TryResolve(baseUrl, rawUrl, out var abs) && map.TryGetValue(abs, out var local))
                parts.Add(local + rest);
            else
                parts.Add(trimmed);
        }
        return string.Join(", ", parts);
    }
}
