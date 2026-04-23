using System.Text.Json;
using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Models;

namespace OpenCrawler.Core.Services;

public class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private AppConfig _current = new();

    public AppConfig Current => _current;

    public bool IsFirstRun =>
        string.IsNullOrWhiteSpace(_current.StorageRoot) || !Directory.Exists(_current.StorageRoot);

    public event EventHandler<AppConfig>? Changed;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var path = AppPaths.ConfigFilePath;
        if (!File.Exists(path))
        {
            _current = new AppConfig();
            return;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            var cfg = await JsonSerializer.DeserializeAsync<AppConfig>(stream, JsonOpts, ct);
            _current = cfg ?? new AppConfig();
        }
        catch
        {
            _current = new AppConfig();
        }
    }

    public void ApplyInMemory(AppConfig cfg)
    {
        _current = cfg;
        Changed?.Invoke(this, cfg);
    }

    public async Task SaveAsync(AppConfig cfg, CancellationToken ct = default)
    {
        Directory.CreateDirectory(AppPaths.ConfigDirectory);
        var tmp = AppPaths.ConfigFilePath + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, cfg, JsonOpts, ct);
        }
        File.Move(tmp, AppPaths.ConfigFilePath, overwrite: true);
        _current = cfg;
        Changed?.Invoke(this, cfg);
    }
}
