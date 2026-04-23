using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCrawler.App.Services;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App.ViewModels;

public partial class FirstRunViewModel : ObservableObject
{
    private readonly IConfigService _cfg;
    private readonly DialogService _dialogs;

    [ObservableProperty] private string _storageRoot = "";
    [ObservableProperty] private bool _canContinue;

    public FirstRunViewModel(IConfigService cfg, DialogService dialogs)
    {
        _cfg = cfg;
        _dialogs = dialogs;
        StorageRoot = cfg.Current.StorageRoot;
        RefreshCanContinue();
    }

    partial void OnStorageRootChanged(string value) => RefreshCanContinue();

    private void RefreshCanContinue()
        => CanContinue = !string.IsNullOrWhiteSpace(StorageRoot) && Directory.Exists(StorageRoot);

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var path = await _dialogs.PickFolderAsync("Select storage folder");
        if (!string.IsNullOrEmpty(path)) StorageRoot = path;
    }

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private async Task ContinueAsync()
    {
        await _cfg.SaveAsync(_cfg.Current with { StorageRoot = StorageRoot });

        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var firstRun = desktop.MainWindow;
            App.InitializeDbAndShowMain(desktop);
            firstRun?.Close();
        }
    }

    partial void OnCanContinueChanged(bool value) => ContinueCommand.NotifyCanExecuteChanged();
}
