using System.Text;
using DbMetaTool.Helpers;
using DbMetaTool.Models;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Services.Exporters;

public class TableExporter
{
    private const string GetAllTablesQueryFile = "GetAllTables.sql";
    private const string GetColumnsQueryFile = "GetTableColumns.sql";
    private const string TablesFolderName = "Tables";

    public void Export(FbConnection conn, string outputDir)
    {
        Console.WriteLine(" -> Pobieranie definicji tabel...");
        string tablesDir = Path.Combine(outputDir, TablesFolderName);
        Directory.CreateDirectory(tablesDir);

        string sql = SqlResourceLoader.Load(GetAllTablesQueryFile);

        using (var cmd = new FbCommand(sql, conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                string tableName = reader.GetString(0).Trim();
                Console.WriteLine($"    -> Przetwarzanie tabeli: {tableName}");

                var columns = GetColumnsForTable(conn, tableName);
                string script = BuildTableScript(tableName, columns);

                File.WriteAllText(Path.Combine(tablesDir, $"{tableName}.sql"), script);
            }
        }
    }

    private List<ColumnDefinition> GetColumnsForTable(FbConnection conn, string tableName)
    {
        var columns = new List<ColumnDefinition>();
        string sql = SqlResourceLoader.Load(GetColumnsQueryFile);

        using (var cmd = new FbCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@TableName", tableName);
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    // Logika mapowania danych z Readera do Obiektu
                    var col = new ColumnDefinition
                    {
                        Name = reader["COLUMN_NAME"].ToString() ?? string.Empty,
                        IsNullable = reader["NULL_FLAG"] == DBNull.Value || Convert.ToInt32(reader["NULL_FLAG"]) == 0
                    };

                    // Tu używamy naszego nowego Helpera!
                    col.DataType = DataTypeMapper.Map(
                        Convert.ToInt16(reader["FIELD_TYPE"]),
                        reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0,
                        Convert.ToInt16(reader["FIELD_SCALE"]),
                        reader["DOMAIN_NAME"].ToString() ?? string.Empty
                    );

                    columns.Add(col);
                }
            }
        }
        return columns;
    }

    private static string BuildTableScript(string tableName, List<ColumnDefinition> columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {tableName} (");
        for (int i = 0; i < columns.Count; i++)
        {
            sb.Append(columns[i].ToString());
            if (i < columns.Count - 1) sb.Append(",");
            sb.AppendLine();
        }
        sb.AppendLine(");");
        return sb.ToString();
    }
}