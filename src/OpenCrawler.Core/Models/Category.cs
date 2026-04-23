using SqlSugar;

namespace OpenCrawler.Core.Models;

[SugarTable("categories")]
public class Category
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnDataType = "INTEGER")]
    public long Id { get; set; }

    [SugarColumn(ColumnName = "parent_id", IsNullable = true)]
    public long? ParentId { get; set; }

    [SugarColumn(ColumnName = "name", IsNullable = false)]
    public string Name { get; set; } = "";

    [SugarColumn(ColumnName = "folder_name", IsNullable = false)]
    public string FolderName { get; set; } = "";

    [SugarColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
