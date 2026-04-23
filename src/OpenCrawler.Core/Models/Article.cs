using SqlSugar;

namespace OpenCrawler.Core.Models;

[SugarTable("articles")]
public class Article
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "category_id")]
    public long CategoryId { get; set; }

    [SugarColumn(ColumnName = "title", IsNullable = false)]
    public string Title { get; set; } = "";

    [SugarColumn(ColumnName = "original_title", IsNullable = true)]
    public string? OriginalTitle { get; set; }

    [SugarColumn(ColumnName = "source_url", IsNullable = false)]
    public string SourceUrl { get; set; } = "";

    [SugarColumn(ColumnName = "folder_name", IsNullable = false)]
    public string FolderName { get; set; } = "";

    [SugarColumn(ColumnName = "fetch_mode", IsNullable = true)]
    public string? FetchMode { get; set; }

    [SugarColumn(ColumnName = "downloaded_at")]
    public DateTime DownloadedAt { get; set; }

    [SugarColumn(ColumnName = "image_count")]
    public int ImageCount { get; set; }

    [SugarColumn(ColumnName = "size_bytes")]
    public long SizeBytes { get; set; }
}
