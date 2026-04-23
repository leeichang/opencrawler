using AngleSharp.Html.Parser;
using OpenCrawler.Core.Services.Fetchers;

namespace OpenCrawler.Core.Services;

public class ChallengeDetector
{
    private static readonly string[] ChallengeMarkers =
    {
        "Just a moment...",
        "Enable JavaScript and cookies",
        "Checking your browser",
        "cf-challenge",
        "cf-browser-verification",
        "Attention Required! | Cloudflare",
        "Please verify you are a human"
    };

    public bool LooksLikeChallenge(FetchResult result)
    {
        if (result.Html.Length < 500) return true;
        foreach (var marker in ChallengeMarkers)
        {
            if (result.Html.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        try
        {
            var parser = new HtmlParser();
            var doc = parser.ParseDocument(result.Html);
            var body = doc.Body;
            if (body == null) return true;

            var visibleText = body.TextContent?.Trim() ?? "";
            var paragraphs = doc.QuerySelectorAll("p, article, section").Count();
            var images = doc.QuerySelectorAll("img[src]").Count();

            // SPA 偵測:body 文字太少 + 幾乎沒有圖/段落 → 疑似需要 JS 渲染
            if (visibleText.Length < 300 && paragraphs < 3 && images < 2)
                return true;
        }
        catch { }

        return false;
    }

    public bool IsAntiBotError(Exception ex) =>
        ex is HttpRequestException http &&
        http.StatusCode is
            System.Net.HttpStatusCode.Forbidden
            or System.Net.HttpStatusCode.TooManyRequests
            or System.Net.HttpStatusCode.ServiceUnavailable;
}
