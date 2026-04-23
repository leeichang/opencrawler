namespace OpenCrawler.Core.Models;

public record AppConfig
{
    public string StorageRoot { get; init; } = "";
    public GcpConfig? Gcp { get; init; }
    public string UiLanguage { get; init; } = "";
    public string Theme { get; init; } = "Fluent";
    public long? LastCategoryId { get; init; }
    public bool UseEmbeddedBrowser { get; init; } = false;
    public string DefaultFetchMode { get; init; } = "auto";
    public bool AllowBrowserMode { get; init; } = true;
    public bool PlaywrightInstalled { get; init; } = false;
}

public record GcpConfig
{
    public string? ServiceAccountJsonPath { get; init; }
    public string ProjectNumber { get; init; } = "";
    public string Location { get; init; } = "global";
    public string EndpointLocation { get; init; } = "global";

    public string GeminiAuthMode { get; init; } = "apiKey";
    public string? GeminiApiKey { get; init; }
    public string GeminiModel { get; init; } = "gemini-2.5-pro";
}
