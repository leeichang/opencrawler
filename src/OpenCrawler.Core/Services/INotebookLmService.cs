namespace OpenCrawler.Core.Services;

public record TextSourceInput(string Name, string Content);

public record AudioOverviewStatus(string State, string? AudioUrl);

public interface INotebookLmService
{
    Task<string> CreateNotebookAsync(string title, CancellationToken ct = default);
    Task<IReadOnlyList<string>> AddTextSourcesAsync(
        string notebookId,
        IReadOnlyList<TextSourceInput> sources,
        CancellationToken ct = default);
    Task<AudioOverviewStatus> StartAudioOverviewAsync(
        string notebookId,
        IReadOnlyList<string>? sourceIds,
        string episodeFocus,
        string languageCode,
        CancellationToken ct = default);
    Task<AudioOverviewStatus> PollAudioOverviewAsync(
        string notebookId,
        CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}

public interface IGeminiSummaryService
{
    Task<string> SummarizeAsync(string prompt, string articleText, CancellationToken ct = default);
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
}
