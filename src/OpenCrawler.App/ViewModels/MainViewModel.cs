using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OpenCrawler.App.Services;
using OpenCrawler.App.Views;
using OpenCrawler.Core.Models;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ICategoryService _categories;
    private readonly IArticleService _articles;
    private readonly DialogService _dialogs;
    private readonly IServiceProvider _sp;

    public ObservableCollection<LibraryNode> Library { get; } = new();

    [ObservableProperty] private LibraryNode? _selectedNode;
    [ObservableProperty] private NotesTabViewModel? _notesTab;

    [ObservableProperty] private string _url = "";
    [ObservableProperty] private string _fetchMode = "auto";
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private Uri? _previewUri;

    public string[] FetchModes { get; } = { "auto", "fast", "browser" };

    public Article? SelectedArticle => SelectedNode is { Kind: LibraryNodeKind.Article, Article: var a } ? a : null;
    public Category? SelectedCategory =>
        SelectedNode switch
        {
            { Kind: LibraryNodeKind.Category, Category: var c } => c,
            { Kind: LibraryNodeKind.Article, Article: var a } when a != null
                => _categories.GetByIdAsync(a.CategoryId).GetAwaiter().GetResult(),
            _ => null
        };

    public MainViewModel(
        ICategoryService categories,
        IArticleService articles,
        DialogService dialogs,
        IServiceProvider sp)
    {
        _categories = categories;
        _articles = articles;
        _dialogs = dialogs;
        _sp = sp;
        _ = LoadLibraryAsync();
    }

    public async Task LoadLibraryAsync()
    {
        Library.Clear();
        var cats = await _categories.GetAllAsync();
        foreach (var c in cats)
        {
            var node = new LibraryNode(c);
            Library.Add(node);
            await LoadChildrenAsync(node);
            node.IsExpanded = true;
        }
    }

    private async Task LoadChildrenAsync(LibraryNode categoryNode)
    {
        if (categoryNode.Kind != LibraryNodeKind.Category) return;
        categoryNode.Children.Clear();
        var articles = await _articles.GetByCategoryAsync(categoryNode.Id);
        foreach (var a in articles)
            categoryNode.Children.Add(new LibraryNode(a));
    }

    async partial void OnSelectedNodeChanged(LibraryNode? value)
    {
        if (value?.Article != null)
        {
            var path = _articles.GetIndexHtmlPath(value.Article);
            PreviewUri = File.Exists(path) ? new Uri(path) : null;

            var tab = _sp.GetRequiredService<NotesTabViewModel>();
            await tab.LoadForArticleAsync(value.Article.Id);
            NotesTab = tab;
        }
        else
        {
            PreviewUri = null;
            NotesTab = null;
        }
        OnPropertyChanged(nameof(SelectedArticle));
        OnPropertyChanged(nameof(SelectedCategory));
    }

    [RelayCommand]
    private async Task NewCategoryAsync()
    {
        var name = await _dialogs.PromptAsync("New category", "Name", $"New {Library.Count + 1}");
        if (string.IsNullOrWhiteSpace(name)) return;
        var cat = await _categories.CreateAsync(name, null);
        Library.Add(new LibraryNode(cat));
    }

    [RelayCommand]
    private async Task RenameNodeAsync(LibraryNode? target)
    {
        var node = target ?? SelectedNode;
        if (node == null) return;
        var name = await _dialogs.PromptAsync("Rename", "New name", node.Title);
        if (string.IsNullOrWhiteSpace(name) || name == node.Title) return;
        if (node.Kind == LibraryNodeKind.Category)
            await _categories.RenameAsync(node.Id, name);
        else
            await _articles.RenameAsync(node.Id, name);
        node.Title = name;
    }

    [RelayCommand]
    private async Task DeleteNodeAsync(LibraryNode? target)
    {
        var node = target ?? SelectedNode;
        if (node == null) return;
        var msg = node.Kind == LibraryNodeKind.Category
            ? $"Delete category '{node.Title}' and all articles?"
            : $"Delete article '{node.Title}'?";
        if (!await _dialogs.ConfirmAsync("Delete", msg)) return;
        if (node.Kind == LibraryNodeKind.Category)
        {
            await _categories.DeleteAsync(node.Id, deleteFiles: true);
            Library.Remove(node);
        }
        else
        {
            await _articles.DeleteAsync(node.Id, deleteFiles: true);
            foreach (var cat in Library) cat.Children.Remove(node);
        }
        if (SelectedNode == node) SelectedNode = null;
    }

    private LibraryNode? GetTargetCategoryNode()
    {
        if (SelectedNode?.Kind == LibraryNodeKind.Category) return SelectedNode;
        if (SelectedNode?.Article != null)
            return Library.FirstOrDefault(n => n.Category?.Id == SelectedNode.Article.CategoryId);
        return Library.FirstOrDefault();
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (string.IsNullOrWhiteSpace(Url)) { StatusMessage = "URL is empty."; return; }
        var catNode = GetTargetCategoryNode();
        if (catNode == null) { StatusMessage = "Select or create a category first."; return; }

        IsDownloading = true;
        StatusMessage = "Downloading...";
        try
        {
            var mode = FetchMode.ToLowerInvariant() switch
            {
                "fast" => Core.Models.FetchMode.Fast,
                "browser" => Core.Models.FetchMode.Browser,
                _ => Core.Models.FetchMode.Auto
            };
            var progress = new Progress<DownloadProgress>(p =>
                StatusMessage = $"{p.Stage} {p.Current}/{p.Total}");
            var article = await _articles.DownloadAsync(Url, catNode.Id, mode, progress, CancellationToken.None);
            var newNode = new LibraryNode(article);
            catNode.Children.Insert(0, newNode);
            catNode.IsExpanded = true;
            SelectedNode = newNode;
            StatusMessage = $"Done ({article.FetchMode}): {article.Title}";
            Url = "";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (SelectedArticle is null) return;
        var path = _articles.GetIndexHtmlPath(SelectedArticle);
        if (File.Exists(path)) OpenWithShell(path);
    }

    [RelayCommand]
    private void RevealInFolder()
    {
        if (SelectedArticle is null) return;
        var folder = _articles.GetArticleFolder(SelectedArticle);
        if (Directory.Exists(folder)) OpenWithShell(folder);
    }

    private static void OpenWithShell(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", $"\"{path}\"");
            else
                Process.Start("xdg-open", path);
        }
        catch { }
    }

    [RelayCommand]
    private async Task SummarizeNodeAsync(LibraryNode? target)
    {
        var node = target ?? SelectedNode;
        var article = node?.Article;
        if (article == null) return;
        var vm = _sp.GetRequiredService<SummaryDialogViewModel>();
        vm.Initialize(article);
        var win = new SummaryDialogWindow { DataContext = vm };
        await OpenDialogAsync(win);
    }

    [RelayCommand]
    private void OpenNodeInBrowser(LibraryNode? target)
    {
        var article = (target ?? SelectedNode)?.Article;
        if (article == null) return;
        var path = _articles.GetIndexHtmlPath(article);
        if (File.Exists(path)) OpenWithShell(path);
    }

    [RelayCommand]
    private void RevealNodeInFolder(LibraryNode? target)
    {
        var article = (target ?? SelectedNode)?.Article;
        if (article == null) return;
        var folder = _articles.GetArticleFolder(article);
        if (Directory.Exists(folder)) OpenWithShell(folder);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var vm = _sp.GetRequiredService<SettingsViewModel>();
        var win = new SettingsWindow { DataContext = vm };
        await OpenDialogAsync(win);
    }

    private Task OpenDialogAsync(Window win)
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        return lifetime?.MainWindow != null ? win.ShowDialog(lifetime.MainWindow) : Task.CompletedTask;
    }
}
