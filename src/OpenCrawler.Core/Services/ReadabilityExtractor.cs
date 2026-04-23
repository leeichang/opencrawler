using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace OpenCrawler.Core.Services;

public class ReadabilityExtractor
{
    private static readonly string[] PrioritySelectors =
    {
        "article",
        "main",
        "[role=main]",
        ".post-content",
        ".article-content",
        ".entry-content",
        "#content"
    };

    public string ExtractPlainText(IHtmlDocument doc)
    {
        foreach (var selector in PrioritySelectors)
        {
            var el = doc.QuerySelector(selector);
            if (el != null && el.TextContent.Trim().Length > 200)
                return Normalize(el.TextContent);
        }
        var body = doc.Body;
        return body != null ? Normalize(body.TextContent) : string.Empty;
    }

    private static string Normalize(string s)
    {
        var lines = s.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l));
        return string.Join("\n", lines);
    }
}
