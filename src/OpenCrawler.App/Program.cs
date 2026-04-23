using Avalonia;
using AvaloniaWebView;
using Avalonia.WebView.Desktop;

namespace OpenCrawler.App;

internal static class Program
{
    [System.STAThread]
    public static int Main(string[] args)
    {
        AvaloniaWebViewBuilder.Initialize(default);
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseDesktopWebView()
            .WithInterFont()
            .LogToTrace();
}
