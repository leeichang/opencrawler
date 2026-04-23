using OpenCrawler.Core.Models;
using SqlSugar;

namespace OpenCrawler.Core.Infrastructure;

public static class DbInitializer
{
    public static void Init(ISqlSugarClient db)
    {
        db.DbMaintenance.CreateDatabase();
        db.CodeFirst.InitTables(
            typeof(Category),
            typeof(Article),
            typeof(ArticleNote),
            typeof(Notebook),
            typeof(NotebookSource),
            typeof(Summary));

        db.Ado.ExecuteCommand(
            "CREATE INDEX IF NOT EXISTS ix_article_notes_article_sort ON article_notes(article_id, sort_order);");
        db.Ado.ExecuteCommand(
            "CREATE INDEX IF NOT EXISTS ix_articles_category ON articles(category_id, downloaded_at DESC);");
        db.Ado.ExecuteCommand(
            "CREATE INDEX IF NOT EXISTS ix_notebooks_scope ON notebooks(scope_type, scope_id);");
    }
}
