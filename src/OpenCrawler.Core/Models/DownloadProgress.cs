namespace OpenCrawler.Core.Models;

public enum DownloadStage
{
    Starting,
    FetchingHtml,
    ParsingHtml,
    DownloadingAssets,
    Writing,
    Completed
}

public record DownloadProgress(
    DownloadStage Stage,
    int Current,
    int Total,
    string? Message = null);

public record DownloadResult(
    string Title,
    string? OriginalTitle,
    string FolderName,
    string IndexHtmlPath,
    string FinalUrl,
    string FetchMode,
    int ImageCount,
    long SizeBytes);

public enum FetchMode
{
    Auto,
    Fast,
    Browser
}
