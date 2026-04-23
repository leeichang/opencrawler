using SqlSugar;

namespace OpenCrawler.Core.Models;

[SugarTable("notebooks")]
public class Notebook
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "scope_type", IsNullable = false)]
    public string ScopeType { get; set; } = "article";

    [SugarColumn(ColumnName = "scope_id")]
    public long ScopeId { get; set; }

    [SugarColumn(ColumnName = "remote_notebook_id", IsNullable = false)]
    public string RemoteNotebookId { get; set; } = "";

    [SugarColumn(ColumnName = "title", IsNullable = false)]
    public string Title { get; set; } = "";

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[SugarTable("sources")]
public class NotebookSource
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "notebook_id")]
    public long NotebookId { get; set; }

    [SugarColumn(ColumnName = "article_id")]
    public long ArticleId { get; set; }

    [SugarColumn(ColumnName = "note_id", IsNullable = true)]
    public long? NoteId { get; set; }

    [SugarColumn(ColumnName = "remote_source_id", IsNullable = false)]
    public string RemoteSourceId { get; set; } = "";

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[SugarTable("summaries")]
public class Summary
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "notebook_id")]
    public long NotebookId { get; set; }

    [SugarColumn(ColumnName = "kind", IsNullable = false)]
    public string Kind { get; set; } = "text";

    [SugarColumn(ColumnName = "prompt", IsNullable = true)]
    public string? Prompt { get; set; }

    [SugarColumn(ColumnName = "content", ColumnDataType = "TEXT", IsNullable = true)]
    public string? Content { get; set; }

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
