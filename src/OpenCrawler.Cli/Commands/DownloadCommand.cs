using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Models;
using OpenCrawler.Core.Services;

namespace OpenCrawler.Cli.Commands;

public static class DownloadCommand
{
    public static Command Build()
    {
        var urlOpt = new Option<string>("--url") { IsRequired = true, Description = "Target URL" };
        var categoryOpt = new Option<string>("--category") { IsRequired = true, Description = "Category folder name" };
        var storageOpt = new Option<string?>("--storage") { IsRequired = false, Description = "Override storage root" };
        var modeOpt = new Option<string>("--mode", () => "auto") { IsRequired = false, Description = "auto | fast | browser" };

        var cmd = new Command("download", "Download an article by URL into a category")
        {
            urlOpt, categoryOpt, storageOpt, modeOpt
        };

        cmd.SetHandler(async (string url, string category, string? storage, string mode) =>
        {
            Environment.ExitCode = await RunAsync(url, category, storage, mode);
        }, urlOpt, categoryOpt, storageOpt, modeOpt);

        return cmd;
    }

    public static async Task<int> RunAsync(string url, string category, string? storageOverride, string mode)
    {
        try
        {
            var services = new ServiceCollection();
            services.AddOpenCrawlerLogging(storageOverride);
            services.AddOpenCrawlerCore();

            await using var provider = services.BuildServiceProvider();

            var cfg = provider.GetRequiredService<IConfigService>();
            await cfg.LoadAsync();
            if (!string.IsNullOrWhiteSpace(storageOverride))
            {
                Directory.CreateDirectory(storageOverride);
                cfg.ApplyInMemory(cfg.Current with { StorageRoot = storageOverride });
            }
            if (string.IsNullOrWhiteSpace(cfg.Current.StorageRoot))
            {
                Console.Error.WriteLine("Storage root is not set. Pass --storage or run the GUI once to configure.");
                return 1;
            }

            var db = provider.GetRequiredService<SqlSugar.ISqlSugarClient>();
            DbInitializer.Init(db);

            var categories = provider.GetRequiredService<ICategoryService>();
            var articles = provider.GetRequiredService<IArticleService>();

            var cat = await categories.EnsureByFolderNameAsync(category);

            var fetchMode = mode?.ToLowerInvariant() switch
            {
                "fast" => FetchMode.Fast,
                "browser" => FetchMode.Browser,
                _ => FetchMode.Auto
            };

            var progress = new Progress<DownloadProgress>(p =>
                Console.Error.WriteLine($"[{p.Stage}] {p.Current}/{p.Total} {p.Message}"));

            var article = await articles.DownloadAsync(url, cat.Id, fetchMode, progress, CancellationToken.None);

            var result = new
            {
                articleId = article.Id,
                title = article.Title,
                folderPath = articles.GetArticleFolder(article),
                imageCount = article.ImageCount,
                sizeBytes = article.SizeBytes,
                fetchMode = article.FetchMode
            };
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = false }));
            return 0;
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Network error: {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"IO error: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 4;
        }
    }
}
