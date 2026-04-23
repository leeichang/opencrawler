using SqlSugar;

namespace OpenCrawler.Core.Models;

[SugarTable("article_notes")]
public class ArticleNote
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "article_id")]
    public long ArticleId { get; set; }

    [SugarColumn(ColumnName = "title", IsNullable = false)]
    public string Title { get; set; } = "";

    [SugarColumn(ColumnName = "content", ColumnDataType = "TEXT", IsNullable = false)]
    public string Content { get; set; } = "";

    [SugarColumn(ColumnName = "sort_order")]
    public int SortOrder { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(ColumnName = "byte_size")]
    public int ByteSize { get; set; }

    [SugarColumn(ColumnName = "char_count")]
    public int CharCount { get; set; }
}
