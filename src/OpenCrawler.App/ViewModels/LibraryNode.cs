using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCrawler.Core.Models;

namespace OpenCrawler.App.ViewModels;

public enum LibraryNodeKind { Category, Article }

public partial class LibraryNode : ObservableObject
{
    public LibraryNodeKind Kind { get; }
    public long Id { get; }
    public Category? Category { get; }
    public Article? Article { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isLoadingChildren;

    public ObservableCollection<LibraryNode> Children { get; } = new();

    public string DisplayLabel => Kind == LibraryNodeKind.Category
        ? $"📁 {Title}"
        : $"📄 {Title}";

    public string Subtitle => Kind == LibraryNodeKind.Article && Article != null
        ? Article.DownloadedAt.ToString("yyyy-MM-dd HH:mm")
        : "";

    public LibraryNode(Category c)
    {
        Kind = LibraryNodeKind.Category;
        Category = c;
        Id = c.Id;
        _title = c.Name;
    }

    public LibraryNode(Article a)
    {
        Kind = LibraryNodeKind.Article;
        Article = a;
        Id = a.Id;
        _title = a.Title;
    }

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayLabel));
}
