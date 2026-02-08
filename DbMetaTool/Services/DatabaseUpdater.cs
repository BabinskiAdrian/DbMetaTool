using FirebirdSql.Data.FirebirdClient;
using DbMetaTool.Services.Interfaces;

namespace DbMetaTool.Services;

public class DatabaseUpdater : IDatabaseUpdater
{
    // Stałe nazw folderów
    private const string FolderDomains = "Domains";
    private const string FolderTables = "Tables";
    private const string FolderProcedures = "Procedures";

    // Stałe zapytań SQL
    private const string SqlCheckDomainExists =
        "SELECT COUNT(*) FROM RDB$FIELDS WHERE RDB$FIELD_NAME = @Name";

    private const string SqlCheckTableExists =
        "SELECT COUNT(*) FROM RDB$RELATIONS WHERE RDB$RELATION_NAME = @Name AND RDB$SYSTEM_FLAG = 0";

    private const string SqlGetExistingColumns =
        "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @TableName";

    // Pomocnicza struktura do przechowywania informacji o kolumnie
    sealed record ColumnInfo(string Name, string FullDefinition);

    public void UpdateDatabase(string connectionString, string scriptsDirectory)
    {
        Console.WriteLine($"[INFO] Rozpoczynam aktualizację bazy...");

        using (var conn = new FbConnection(connectionString))
        {
            conn.Open();

            // 1. Domeny (Dodajemy tylko nowe)
            UpdateDomains(conn, scriptsDirectory);

            // 2. Tabele (Tworzymy nowe LUB dodajemy kolumny do istniejących)
            UpdateTables(conn, scriptsDirectory);

            // 3. Procedury (Nadpisujemy CREATE OR ALTER)
            UpdateProcedures(conn, scriptsDirectory);
        }
        Console.WriteLine("[SUKCES] Aktualizacja zakończona.");
    }

    private static void UpdateDomains(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderDomains);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string domainName = Path.GetFileNameWithoutExtension(file);

            // Sprawdź, czy domena istnieje
            if (!EntityExists(conn, SqlCheckDomainExists, domainName))
            {
                Console.WriteLine($" -> Dodawanie nowej domeny: {domainName}");
                ExecuteSql(conn, script);
            }
        }
    }

    private static void UpdateTables(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderTables);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string tableName = Path.GetFileNameWithoutExtension(file);

            if (!EntityExists(conn, SqlCheckTableExists, tableName))
            {
                Console.WriteLine($" -> Tworzenie nowej tabeli: {tableName}");
                ExecuteSql(conn, script);
            }
            else
            {
                UpdateTableColumns(conn, tableName, script);
            }
        }
    }

    private static void UpdateProcedures(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderProcedures);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string procName = Path.GetFileNameWithoutExtension(file);

            Console.WriteLine($" -> Aktualizacja procedury: {procName}");

            ExecuteSql(conn, script);
        }
    }

    private static void UpdateTableColumns(FbConnection conn, string tableName, string script)
    {
        // Parsujemy skrypt, żeby wyciągnąć definicje kolumn
        var columnsInScript = ParseColumnsFromScript(script);

        // Pobieramy istniejące kolumny z bazy
        var existingColumns = GetExistingColumns(conn, tableName);

        // UWAGA: rozważyć pod co zoptymalizować.
        var columnsToAdd = columnsInScript
            .Where(c => !existingColumns.Contains(c.Name.ToUpper()))
            .ToList(); // !Materializacja

        foreach (var col in columnsToAdd)
        { 
            Console.WriteLine($"    -> [UPDATE] Dodawanie kolumny {col.Name} do tabeli {tableName}");

            // Budujemy ALTER TABLE
            string alterSql = $"ALTER TABLE {tableName} ADD {col.FullDefinition}";

            try
            {
                ExecuteSql(conn, alterSql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Nie udało się dodać kolumny {col.Name}: {ex.Message}");
            }
        }
    }

    private static bool EntityExists(FbConnection conn, string sql, string name)
    {
        using (var cmd = new FbCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@Name", name.ToUpper());
            var result = cmd.ExecuteScalar();
            return result != null && Convert.ToInt32(result) > 0;
        }
    }

    private static HashSet<string> GetExistingColumns(FbConnection conn, string tableName)
    {
        var cols = new HashSet<string>();

        using (var cmd = new FbCommand(SqlGetExistingColumns, conn))
        {
            cmd.Parameters.AddWithValue("@TableName", tableName.ToUpper());
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    cols.Add(reader.GetString(0).Trim().ToUpper());
                }
            }
        }
        return cols;
    }

    private static void ExecuteSql(FbConnection conn, string sql)
    {
        using (var cmd = new FbCommand(sql, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    private static List<ColumnInfo> ParseColumnsFromScript(string script)
    {
        var list = new List<ColumnInfo>();

        // Rozbijamy skrypt na linie
        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            string trimmed = line.Trim();

            // Pomijamy linie "CREATE TABLE", nawiasy, puste itp.
            if (trimmed.StartsWith("CREATE TABLE") 
                || trimmed == "(" 
                || trimmed == ");" 
                || trimmed == ")")
            { 
                continue;
            }

            // Usuwamy ewentualny przecinek na końcu linii
            if (trimmed.EndsWith(','))
            { 
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            // Parsowanie: Pierwsze słowo to nazwa kolumny, reszta to definicja
            var parts = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                list.Add(new ColumnInfo(parts[0], trimmed));
            }
        }
        return list;
    }
}