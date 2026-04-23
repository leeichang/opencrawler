using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCrawler.App.Services;
using OpenCrawler.Core.Models;
using OpenCrawler.Core.Resources;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigService _cfg;
    private readonly DialogService _dialogs;
    private readonly INotebookLmService _notebookLm;
    private readonly IGeminiSummaryService _gemini;

    public string[] Languages { get; } = { "zh-TW", "en" };
    public string[] Locations { get; } = { "global", "us", "eu" };
    public string[] Themes { get; } = { "Fluent", "Dark" };

    [ObservableProperty] private string _storageRoot = "";
    [ObservableProperty] private string _uiLanguage = "zh-TW";
    [ObservableProperty] private string _theme = "Fluent";

    [ObservableProperty] private string _serviceAccountJsonPath = "";
    [ObservableProperty] private string _projectNumber = "";
    [ObservableProperty] private string _endpointLocation = "global";
    [ObservableProperty] private string _location = "global";

    [ObservableProperty] private string _geminiApiKey = "";
    [ObservableProperty] private string _geminiModel = "gemini-2.5-pro";

    [ObservableProperty] private string _gcpTestResult = "";
    [ObservableProperty] private string _geminiTestResult = "";

    public SettingsViewModel(
        IConfigService cfg,
        DialogService dialogs,
        INotebookLmService notebookLm,
        IGeminiSummaryService gemini)
    {
        _cfg = cfg;
        _dialogs = dialogs;
        _notebookLm = notebookLm;
        _gemini = gemini;

        var c = cfg.Current;
        StorageRoot = c.StorageRoot;
        UiLanguage = string.IsNullOrEmpty(c.UiLanguage) ? "zh-TW" : c.UiLanguage;
        Theme = c.Theme;
        ServiceAccountJsonPath = c.Gcp?.ServiceAccountJsonPath ?? "";
        ProjectNumber = c.Gcp?.ProjectNumber ?? "";
        EndpointLocation = c.Gcp?.EndpointLocation ?? "global";
        Location = c.Gcp?.Location ?? "global";
        GeminiApiKey = c.Gcp?.GeminiApiKey ?? "";
        GeminiModel = c.Gcp?.GeminiModel ?? "gemini-2.5-pro";
    }

    [RelayCommand]
    private async Task PickStorageAsync()
    {
        var p = await _dialogs.PickFolderAsync("Select storage folder");
        if (!string.IsNullOrEmpty(p)) StorageRoot = p;
    }

    [RelayCommand]
    private async Task PickSaJsonAsync()
    {
        var p = await _dialogs.PickFileAsync("Select Service Account JSON", "JSON", "json");
        if (!string.IsNullOrEmpty(p)) ServiceAccountJsonPath = p;
    }

    [RelayCommand]
    private void ChangeLanguage()
    {
        LocalizationManager.Instance.SetCulture(CultureInfo.GetCultureInfo(UiLanguage));
    }

    [RelayCommand]
    private async Task TestGcpAsync()
    {
        await ApplyAsync();
        GcpTestResult = "Testing...";
        var ok = await _notebookLm.TestConnectionAsync();
        GcpTestResult = ok ? "OK" : "Failed (see log)";
    }

    [RelayCommand]
    private async Task TestGeminiAsync()
    {
        await ApplyAsync();
        GeminiTestResult = "Testing...";
        var ok = await _gemini.TestConnectionAsync();
        GeminiTestResult = ok ? "OK" : "Failed (see log)";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ApplyAsync();
        LocalizationManager.Instance.SetCulture(CultureInfo.GetCultureInfo(UiLanguage));
    }

    private Task ApplyAsync()
    {
        var updated = _cfg.Current with
        {
            StorageRoot = StorageRoot,
            UiLanguage = UiLanguage,
            Theme = Theme,
            Gcp = new GcpConfig
            {
                ServiceAccountJsonPath = string.IsNullOrWhiteSpace(ServiceAccountJsonPath) ? null : ServiceAccountJsonPath,
                ProjectNumber = ProjectNumber,
                EndpointLocation = EndpointLocation,
                Location = Location,
                GeminiApiKey = string.IsNullOrWhiteSpace(GeminiApiKey) ? null : GeminiApiKey,
                GeminiModel = GeminiModel
            }
        };
        return _cfg.SaveAsync(updated);
    }
}
