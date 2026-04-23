using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenCrawler.Core.Services;
using OpenCrawler.Core.Services.Fetchers;
using Serilog;
using SqlSugar;

namespace OpenCrawler.Core.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOpenCrawlerCore(this IServiceCollection services)
    {
        services.AddSingleton<IConfigService, ConfigService>();

        services.AddSingleton<ISqlSugarClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfigService>();
            var root = cfg.Current.StorageRoot;
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("StorageRoot not configured; call ConfigService.LoadAsync first.");
            return SqlSugarFactory.Create(AppPaths.DbFilePath(root));
        });

        services.AddHttpClient();
        services.AddSingleton<FastHtmlFetcher>();
        services.AddSingleton<BrowserHtmlFetcher>();
        services.AddSingleton<ChallengeDetector>();
        services.AddSingleton<ReadabilityExtractor>();

        services.AddTransient<IWebDownloader>(sp => new WebDownloader(
            sp.GetRequiredService<FastHtmlFetcher>(),
            sp.GetRequiredService<BrowserHtmlFetcher>(),
            sp.GetRequiredService<ChallengeDetector>(),
            sp.GetRequiredService<ReadabilityExtractor>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("assets"),
            sp.GetRequiredService<ILogger<WebDownloader>>()));

        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IArticleService, ArticleService>();
        services.AddSingleton<IArticleNoteService, ArticleNoteService>();

        services.AddTransient<INotebookLmService>(sp => new NotebookLmService(
            sp.GetRequiredService<IConfigService>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("notebooklm"),
            sp.GetRequiredService<ILogger<NotebookLmService>>()));
        services.AddTransient<IGeminiSummaryService>(sp => new GeminiSummaryService(
            sp.GetRequiredService<IConfigService>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("gemini"),
            sp.GetRequiredService<ILogger<GeminiSummaryService>>()));

        return services;
    }

    public static IServiceCollection AddOpenCrawlerLogging(this IServiceCollection services, string? storageRoot)
    {
        var loggerConfig = new LoggerConfiguration().MinimumLevel.Information();
        if (!string.IsNullOrWhiteSpace(storageRoot))
        {
            var logDir = AppPaths.LogDirectory(storageRoot);
            Directory.CreateDirectory(logDir);
            loggerConfig = loggerConfig.WriteTo.File(
                Path.Combine(logDir, "opencrawler-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);
        }
        Log.Logger = loggerConfig.CreateLogger();
        services.AddLogging(lb => lb.AddSerilog(dispose: true));
        return services;
    }
}
