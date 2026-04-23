namespace OpenCrawler.Core.Services.Fetchers;

public record FetchResult(
    string Html,
    string FinalUrl,
    IReadOnlyDictionary<string, string> Headers);

public interface IHtmlFetcher
{
    string ModeName { get; }
    Task<FetchResult> FetchAsync(string url, CancellationToken ct);
}
