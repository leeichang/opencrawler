using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCrawler.Core.Models;

namespace OpenCrawler.App.ViewModels;

public partial class NoteTabItem : ObservableObject
{
    public ArticleNote Model { get; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _content;
    [ObservableProperty] private int _byteSize;
    [ObservableProperty] private string _sizeLevel = "Ok";
    [ObservableProperty] private string _statusText = "";

    public long Id => Model.Id;
    public const long SoftLimit = 2L * 1024 * 1024;

    public NoteTabItem(ArticleNote model)
    {
        Model = model;
        _title = model.Title;
        _content = model.Content;
        _byteSize = model.ByteSize;
        RecomputeLevel();
    }

    partial void OnContentChanged(string value)
    {
        ByteSize = Encoding.UTF8.GetByteCount(value ?? "");
        RecomputeLevel();
    }

    private void RecomputeLevel()
    {
        var pct = ByteSize * 100.0 / SoftLimit;
        SizeLevel = pct switch
        {
            < 80 => "Ok",
            < 95 => "Warning",
            < 100 => "Danger",
            _ => "Over"
        };
    }

    public string SizeDisplay => $"{ByteSize / 1024.0:F1} KB / 2 MB";
}
