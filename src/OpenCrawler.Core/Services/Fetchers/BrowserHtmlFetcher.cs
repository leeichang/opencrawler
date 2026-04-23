using Microsoft.Playwright;

namespace OpenCrawler.Core.Services.Fetchers;

public class BrowserHtmlFetcher : IHtmlFetcher, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initGate = new(1, 1);

    public string ModeName => "browser";

    public async Task<FetchResult> FetchAsync(string url, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                        "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            Locale = "zh-TW"
        });

        try
        {
            var page = await context.NewPageAsync();
            var response = await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30_000
            });

            var html = await page.ContentAsync();
            var finalUrl = page.Url;

            var headers = new Dictionary<string, string>();
            if (response != null)
            {
                foreach (var (k, v) in await response.AllHeadersAsync())
                    headers[k] = v;
            }
            return new FetchResult(html, finalUrl, headers);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_browser != null) return;
        await _initGate.WaitAsync(ct);
        try
        {
            if (_browser != null) return;
            _playwright ??= await Playwright.CreateAsync();
            _browser = await LaunchWithAutoInstallAsync(_playwright);
        }
        finally { _initGate.Release(); }
    }

    private static async Task<IBrowser> LaunchWithAutoInstallAsync(IPlaywright pw)
    {
        try
        {
            return await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
        }
        catch (Exception ex) when (ex.Message.Contains("Executable doesn't exist", StringComparison.OrdinalIgnoreCase)
                                 || ex.Message.Contains("playwright install", StringComparison.OrdinalIgnoreCase))
        {
            // First-run: install Chromium
            await Task.Run(() => Program.Main(new[] { "install", "chromium" }));
            return await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--disable-blink-features=AutomationControlled" }
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }
}
