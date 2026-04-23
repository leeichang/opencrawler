using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default);
    Task<Category?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Category> CreateAsync(string name, long? parentId, CancellationToken ct = default);
    Task<Category> EnsureByFolderNameAsync(string folderName, CancellationToken ct = default);
    Task RenameAsync(long id, string newName, CancellationToken ct = default);
    Task DeleteAsync(long id, bool deleteFiles, CancellationToken ct = default);
    string GetCategoryPath(Category category);
}
