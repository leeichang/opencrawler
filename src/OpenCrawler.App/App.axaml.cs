using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using OpenCrawler.App.Services;
using OpenCrawler.App.ViewModels;
using OpenCrawler.App.Views;
using OpenCrawler.Core.Infrastructure;
using OpenCrawler.Core.Resources;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App;

public class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddOpenCrawlerLogging(storageRoot: null);
        services.AddOpenCrawlerCore();
        services.AddSingleton<DialogService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<FirstRunViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<NotesTabViewModel>();
        services.AddTransient<SummaryDialogViewModel>();
        Services = services.BuildServiceProvider();

        var cfg = Services.GetRequiredService<IConfigService>();
        await cfg.LoadAsync();
        LocalizationManager.Instance.SetCultureByName(cfg.Current.UiLanguage);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (cfg.IsFirstRun)
            {
                var first = new FirstRunWindow { DataContext = Services.GetRequiredService<FirstRunViewModel>() };
                desktop.MainWindow = first;
            }
            else
            {
                InitializeDbAndShowMain(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void InitializeDbAndShowMain(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var db = Services.GetRequiredService<SqlSugar.ISqlSugarClient>();
        DbInitializer.Init(db);
        var main = new MainWindow { DataContext = Services.GetRequiredService<MainViewModel>() };
        desktop.MainWindow = main;
        main.Show();
    }
}
