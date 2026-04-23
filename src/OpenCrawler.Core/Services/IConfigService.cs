using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public interface IConfigService
{
    AppConfig Current { get; }
    bool IsFirstRun { get; }
    event EventHandler<AppConfig>? Changed;

    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppConfig cfg, CancellationToken ct = default);
    void ApplyInMemory(AppConfig cfg);
}
