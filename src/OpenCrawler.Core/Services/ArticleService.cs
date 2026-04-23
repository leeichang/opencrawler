using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Models;
using SqlSugar;

namespace OpenCrawler.Core.Services;

public class ArticleService : IArticleService
{
    private readonly ISqlSugarClient _db;
    private readonly IConfigService _cfg;
    private readonly ICategoryService _categories;
    private readonly IWebDownloader _downloader;

    public ArticleService(
        ISqlSugarClient db,
        IConfigService cfg,
        ICategoryService categories,
        IWebDownloader downloader)
    {
        _db = db;
        _cfg = cfg;
        _categories = categories;
        _downloader = downloader;
    }

    public async Task<IReadOnlyList<Article>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        var list = await _db.Queryable<Article>()
            .Where(a => a.CategoryId == categoryId)
            .OrderByDescending(a => a.DownloadedAt)
            .ToListAsync();
        return list;
    }

    public Task<Article?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.Queryable<Article>().Where(a => a.Id == id).FirstAsync()!;

    public async Task<Article> DownloadAsync(
        string url,
        long categoryId,
        FetchMode mode,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(categoryId, ct)
            ?? throw new InvalidOperationException($"Category {categoryId} not found");

        var categoryPath = _categories.GetCategoryPath(category);
        Directory.CreateDirectory(categoryPath);

        var folderName = FileNameSanitizer.GenerateArticleFolderName(null);
        var articleFolder = Path.Combine(categoryPath, folderName);

        var result = await _downloader.DownloadAsync(url, articleFolder, mode, progress, ct);

        if (Path.GetFileName(articleFolder) != result.FolderName)
        {
            folderName = result.FolderName;
        }

        var article = new Article
        {
            CategoryId = categoryId,
            Title = result.Title,
            OriginalTitle = result.OriginalTitle,
            SourceUrl = url,
            FolderName = folderName,
            FetchMode = result.FetchMode,
            DownloadedAt = DateTime.UtcNow,
            ImageCount = result.ImageCount,
            SizeBytes = result.SizeBytes
        };
        article.Id = await _db.Insertable(article).ExecuteReturnBigIdentityAsync();

        var defaultNoteTitle = _cfg.Current.UiLanguage == "en" ? "Default Note" : "預設註解";
        await _db.Insertable(new ArticleNote
        {
            ArticleId = article.Id,
            Title = defaultNoteTitle,
            Content = "",
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }).ExecuteCommandAsync();

        return article;
    }

    public async Task RenameAsync(long id, string newTitle, CancellationToken ct = default)
    {
        await _db.Updateable<Article>()
            .SetColumns(a => new Article { Title = newTitle })
            .Where(a => a.Id == id)
            .ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id, bool deleteFiles, CancellationToken ct = default)
    {
        var article = await GetByIdAsync(id, ct);
        if (article == null) return;

        await _db.Deleteable<ArticleNote>().Where(n => n.ArticleId == id).ExecuteCommandAsync();
        await _db.Deleteable<Article>().Where(a => a.Id == id).ExecuteCommandAsync();

        if (deleteFiles)
        {
            var folder = GetArticleFolder(article);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    public string GetArticleFolder(Article article)
    {
        var category = _db.Queryable<Category>().Where(c => c.Id == article.CategoryId).First()
            ?? throw new InvalidOperationException($"Category {article.CategoryId} not found");
        return Path.Combine(_categories.GetCategoryPath(category), article.FolderName);
    }

    public string GetIndexHtmlPath(Article article)
        => Path.Combine(GetArticleFolder(article), "index.html");
}
