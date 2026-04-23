using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public interface IWebDownloader
{
    Task<DownloadResult> DownloadAsync(
        string url,
        string targetFolder,
        FetchMode mode,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct);
}
