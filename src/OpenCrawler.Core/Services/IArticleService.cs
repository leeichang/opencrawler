using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public interface IArticleService
{
    Task<IReadOnlyList<Article>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
    Task<Article?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Article> DownloadAsync(
        string url,
        long categoryId,
        FetchMode mode,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default);
    Task RenameAsync(long id, string newTitle, CancellationToken ct = default);
    Task DeleteAsync(long id, bool deleteFiles, CancellationToken ct = default);
    string GetArticleFolder(Article article);
    string GetIndexHtmlPath(Article article);
}
