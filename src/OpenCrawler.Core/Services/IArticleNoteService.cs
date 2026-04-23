using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public interface IArticleNoteService
{
    Task<IReadOnlyList<ArticleNote>> ListAsync(long articleId, CancellationToken ct = default);
    Task<ArticleNote> CreateAsync(long articleId, string title, CancellationToken ct = default);
    Task RenameAsync(long noteId, string newTitle, CancellationToken ct = default);
    Task ReorderAsync(long articleId, IReadOnlyList<long> orderedIds, CancellationToken ct = default);
    Task DeleteAsync(long noteId, CancellationToken ct = default);
    Task ScheduleSaveAsync(long noteId, string content);
    Task SaveNowAsync(long noteId, string content, CancellationToken ct = default);
}
