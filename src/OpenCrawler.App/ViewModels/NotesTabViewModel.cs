using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App.ViewModels;

public partial class NotesTabViewModel : ObservableObject
{
    private readonly IArticleNoteService _notes;

    [ObservableProperty] private long _articleId;
    public ObservableCollection<NoteTabItem> Notes { get; } = new();

    [ObservableProperty] private NoteTabItem? _current;
    [ObservableProperty] private bool _isLoaded;

    public NotesTabViewModel(IArticleNoteService notes)
    {
        _notes = notes;
    }

    public async Task LoadForArticleAsync(long articleId)
    {
        if (ArticleId == articleId && IsLoaded) return;
        ArticleId = articleId;
        Notes.Clear();
        var list = await _notes.ListAsync(articleId);
        foreach (var n in list) Notes.Add(new NoteTabItem(n));
        Current = Notes.FirstOrDefault();
        IsLoaded = true;
    }

    partial void OnCurrentChanged(NoteTabItem? oldValue, NoteTabItem? newValue)
    {
        if (oldValue != null)
        {
            _ = _notes.SaveNowAsync(oldValue.Id, oldValue.Content, CancellationToken.None);
        }
    }

    [RelayCommand]
    private async Task AddNoteAsync()
    {
        var title = $"Note {Notes.Count + 1}";
        var note = await _notes.CreateAsync(ArticleId, title);
        var item = new NoteTabItem(note);
        Notes.Add(item);
        Current = item;
    }

    [RelayCommand]
    private async Task DeleteCurrentAsync()
    {
        if (Current == null || Notes.Count <= 1) return;
        var toRemove = Current;
        await _notes.DeleteAsync(toRemove.Id);
        Notes.Remove(toRemove);
        Current = Notes.FirstOrDefault();
    }

    public Task OnContentChangedAsync(NoteTabItem item)
    {
        item.StatusText = "未儲存的變更";
        return _notes.ScheduleSaveAsync(item.Id, item.Content);
    }

    public Task SaveAllAsync()
    {
        var tasks = Notes.Select(n => _notes.SaveNowAsync(n.Id, n.Content, CancellationToken.None));
        return Task.WhenAll(tasks);
    }
}
