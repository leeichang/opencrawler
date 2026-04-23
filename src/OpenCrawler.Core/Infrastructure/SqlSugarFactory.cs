using SqlSugar;

namespace OpenCrawler.Core.Infrastructure;

public static class SqlSugarFactory
{
    public static ISqlSugarClient Create(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var cs = $"Data Source={dbPath};Foreign Keys=True;";
        var scope = new SqlSugarScope(new ConnectionConfig
        {
            DbType = DbType.Sqlite,
            ConnectionString = cs,
            IsAutoCloseConnection = true,
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityService = (_, col) =>
                {
                    if (col.IsPrimarykey == false && col.PropertyInfo.PropertyType.IsGenericType
                        && col.PropertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        col.IsNullable = true;
                    }
                }
            }
        });

        scope.Ado.ExecuteCommand("PRAGMA journal_mode=WAL;");
        return scope;
    }
}
