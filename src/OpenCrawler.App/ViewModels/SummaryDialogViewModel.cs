using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCrawler.Core.Models;
using OpenCrawler.Core.Services;

namespace OpenCrawler.App.ViewModels;

public partial class SummaryDialogViewModel : ObservableObject
{
    private readonly INotebookLmService _nb;
    private readonly IGeminiSummaryService _gem;
    private readonly IArticleService _articles;
    private readonly IArticleNoteService _noteSvc;

    [ObservableProperty] private long _articleId;
    [ObservableProperty] private string _articleTitle = "";
    [ObservableProperty] private string _episodeFocus = "請為這篇文章做重點摘要,突出關鍵結論與可行動的建議。";
    [ObservableProperty] private bool _includeNotes = true;
    [ObservableProperty] private bool _generateAudio = true;
    [ObservableProperty] private bool _generateText = true;

    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _textSummary = "";
    [ObservableProperty] private string _audioUrl = "";
    [ObservableProperty] private bool _isRunning;

    public SummaryDialogViewModel(
        INotebookLmService nb,
        IGeminiSummaryService gem,
        IArticleService articles,
        IArticleNoteService noteSvc)
    {
        _nb = nb;
        _gem = gem;
        _articles = articles;
        _noteSvc = noteSvc;
    }

    public void Initialize(Article a)
    {
        ArticleId = a.Id;
        ArticleTitle = a.Title;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning) return;
        IsRunning = true;
        Status = "Starting...";
        try
        {
            var article = await _articles.GetByIdAsync(ArticleId);
            if (article == null) { Status = "Article not found"; return; }

            var folder = _articles.GetArticleFolder(article);
            var contentPath = Path.Combine(folder, "content.txt");
            var text = File.Exists(contentPath) ? await File.ReadAllTextAsync(contentPath) : "";
            var sources = new List<TextSourceInput> { new($"{article.Title}", Trim(text)) };

            if (IncludeNotes)
            {
                var notes = await _noteSvc.ListAsync(ArticleId);
                foreach (var n in notes.Where(n => !string.IsNullOrWhiteSpace(n.Content)))
                    sources.Add(new TextSourceInput($"{article.Title} - {n.Title}", Trim(n.Content)));
            }

            Task<string>? geminiTask = null;
            if (GenerateText)
            {
                Status = "Gemini 文字摘要中...";
                geminiTask = _gem.SummarizeAsync(EpisodeFocus, sources[0].Content, CancellationToken.None);
            }

            string? notebookId = null;
            if (GenerateAudio)
            {
                Status = "建立 NotebookLM...";
                notebookId = await _nb.CreateNotebookAsync($"openCrawler · {article.Title}", CancellationToken.None);
                Status = $"上傳 {sources.Count} 個 source...";
                var sourceIds = await _nb.AddTextSourcesAsync(notebookId, sources, CancellationToken.None);
                Status = "開始音訊摘要...";
                await _nb.StartAudioOverviewAsync(notebookId, sourceIds, EpisodeFocus, "zh-TW", CancellationToken.None);
                _ = PollAudioAsync(notebookId);
            }

            if (geminiTask != null)
            {
                TextSummary = await geminiTask;
                Status = "文字摘要完成";
            }
            else
            {
                Status = "已送出,請稍後查看音訊";
            }
        }
        catch (Exception ex)
        {
            Status = $"Error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task PollAudioAsync(string notebookId)
    {
        var deadline = DateTime.UtcNow.AddMinutes(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            try
            {
                var st = await _nb.PollAudioOverviewAsync(notebookId, CancellationToken.None);
                if (!string.IsNullOrEmpty(st.AudioUrl))
                {
                    AudioUrl = st.AudioUrl;
                    Status = "音訊摘要完成";
                    return;
                }
            }
            catch { }
        }
        Status = "音訊生成時間較長,請稍後到 NotebookLM 網頁查看";
    }

    private static string Trim(string s, int max = 100_000)
        => s.Length <= max ? s : s[..max] + "\n\n...(內容已截斷)";
}
