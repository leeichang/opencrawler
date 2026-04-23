using CommunityToolkit.Mvvm.ComponentModel;
using OpenCrawler.Core.Models;

namespace OpenCrawler.App.ViewModels;

public partial class ArticleItem : ObservableObject
{
    public Article Model { get; }
    [ObservableProperty] private string _title;
    public long Id => Model.Id;
    public string SourceUrl => Model.SourceUrl;
    public DateTime DownloadedAt => Model.DownloadedAt;
    public int ImageCount => Model.ImageCount;
    public long SizeBytes => Model.SizeBytes;

    public ArticleItem(Article model)
    {
        Model = model;
        _title = model.Title;
    }
}
