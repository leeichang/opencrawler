using System.Collections.Concurrent;
using System.Text;
using OpenCrawler.Core.Models;
using SqlSugar;

namespace OpenCrawler.Core.Services;

public class ArticleNoteService : IArticleNoteService
{
    private readonly ISqlSugarClient _db;
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _debounces = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(2);

    public ArticleNoteService(ISqlSugarClient db) => _db = db;

    public async Task<IReadOnlyList<ArticleNote>> ListAsync(long articleId, CancellationToken ct = default)
    {
        var list = await _db.Queryable<ArticleNote>()
            .Where(n => n.ArticleId == articleId)
            .OrderBy(n => n.SortOrder)
            .OrderBy(n => n.Id)
            .ToListAsync();
        return list;
    }

    public async Task<ArticleNote> CreateAsync(long articleId, string title, CancellationToken ct = default)
    {
        var maxOrder = await _db.Queryable<ArticleNote>()
            .Where(n => n.ArticleId == articleId)
            .MaxAsync(n => (int?)n.SortOrder) ?? -1;
        var note = new ArticleNote
        {
            ArticleId = articleId,
            Title = title,
            Content = "",
            SortOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        note.Id = await _db.Insertable(note).ExecuteReturnBigIdentityAsync();
        return note;
    }

    public async Task RenameAsync(long noteId, string newTitle, CancellationToken ct = default)
    {
        await _db.Updateable<ArticleNote>()
            .SetColumns(n => new ArticleNote { Title = newTitle, UpdatedAt = DateTime.UtcNow })
            .Where(n => n.Id == noteId)
            .ExecuteCommandAsync();
    }

    public async Task ReorderAsync(long articleId, IReadOnlyList<long> orderedIds, CancellationToken ct = default)
    {
        for (var i = 0; i < orderedIds.Count; i++)
        {
            var id = orderedIds[i];
            var order = i;
            await _db.Updateable<ArticleNote>()
                .SetColumns(n => new ArticleNote { SortOrder = order })
                .Where(n => n.Id == id && n.ArticleId == articleId)
                .ExecuteCommandAsync();
        }
    }

    public async Task DeleteAsync(long noteId, CancellationToken ct = default)
    {
        await _db.Deleteable<ArticleNote>().Where(n => n.Id == noteId).ExecuteCommandAsync();
    }

    public Task ScheduleSaveAsync(long noteId, string content)
    {
        var cts = new CancellationTokenSource();
        _debounces.AddOrUpdate(noteId, cts, (_, old) => { old.Cancel(); return cts; });
        var token = cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(Debounce, token);
                await SaveNowAsync(noteId, content, token);
            }
            catch (TaskCanceledException) { }
        }, token);
        return Task.CompletedTask;
    }

    public async Task SaveNowAsync(long noteId, string content, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var bytes = Encoding.UTF8.GetByteCount(content);
            var now = DateTime.UtcNow;
            await _db.Updateable<ArticleNote>()
                .SetColumns(n => new ArticleNote
                {
                    Content = content,
                    UpdatedAt = now,
                    ByteSize = bytes,
                    CharCount = content.Length
                })
                .Where(n => n.Id == noteId)
                .ExecuteCommandAsync();
        }
        finally { _gate.Release(); }
    }
}
