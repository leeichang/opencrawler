using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Models;
using SqlSugar;

namespace OpenCrawler.Core.Services;

public class CategoryService : ICategoryService
{
    private readonly ISqlSugarClient _db;
    private readonly IConfigService _cfg;

    public CategoryService(ISqlSugarClient db, IConfigService cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
    {
        var list = await _db.Queryable<Category>().OrderBy(c => c.Name).ToListAsync();
        return list;
    }

    public Task<Category?> GetByIdAsync(long id, CancellationToken ct = default)
        => _db.Queryable<Category>().Where(c => c.Id == id).FirstAsync()!;

    public async Task<Category> CreateAsync(string name, long? parentId, CancellationToken ct = default)
    {
        var folder = FileNameSanitizer.Sanitize(name, 80);
        var cat = new Category
        {
            Name = name,
            ParentId = parentId,
            FolderName = folder,
            CreatedAt = DateTime.UtcNow
        };
        cat.Id = await _db.Insertable(cat).ExecuteReturnBigIdentityAsync();

        var path = GetCategoryPath(cat);
        Directory.CreateDirectory(path);
        return cat;
    }

    public async Task<Category> EnsureByFolderNameAsync(string folderName, CancellationToken ct = default)
    {
        var sanitized = FileNameSanitizer.Sanitize(folderName, 80);
        var existing = await _db.Queryable<Category>()
            .Where(c => c.FolderName == sanitized && c.ParentId == null)
            .FirstAsync();
        if (existing != null) return existing;

        return await CreateAsync(folderName, parentId: null, ct);
    }

    public async Task RenameAsync(long id, string newName, CancellationToken ct = default)
    {
        await _db.Updateable<Category>()
            .SetColumns(c => new Category { Name = newName })
            .Where(c => c.Id == id)
            .ExecuteCommandAsync();
    }

    public async Task DeleteAsync(long id, bool deleteFiles, CancellationToken ct = default)
    {
        var cat = await GetByIdAsync(id, ct);
        if (cat == null) return;

        var articles = await _db.Queryable<Article>().Where(a => a.CategoryId == id).ToListAsync();
        foreach (var a in articles)
        {
            await _db.Deleteable<ArticleNote>().Where(n => n.ArticleId == a.Id).ExecuteCommandAsync();
        }
        await _db.Deleteable<Article>().Where(a => a.CategoryId == id).ExecuteCommandAsync();
        await _db.Deleteable<Category>().Where(c => c.Id == id).ExecuteCommandAsync();

        if (deleteFiles)
        {
            var path = GetCategoryPath(cat);
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }

    public string GetCategoryPath(Category category)
    {
        var root = _cfg.Current.StorageRoot;
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("StorageRoot is not configured.");
        return Path.Combine(root, category.FolderName);
    }
}
