using FirebirdSql.Data.FirebirdClient;
using DbMetaTool.Services.Interfaces;

namespace DbMetaTool.Services;

public class DatabaseUpdater : IDatabaseUpdater
{
    private const string FolderDomains = "Domains";
    private const string FolderTables = "Tables";
    private const string FolderProcedures = "Procedures";

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

    private void UpdateDomains(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderDomains);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string domainName = Path.GetFileNameWithoutExtension(file);

            // Sprawdź, czy domena istnieje
            if (!ObjectExists(conn, domainName, "RDB$FIELDS"))
            {
                Console.WriteLine($" -> Dodawanie nowej domeny: {domainName}");
                ExecuteSql(conn, script);
            }
        }
    }

    private void UpdateProcedures(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderProcedures);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string procName = Path.GetFileNameWithoutExtension(file);

            Console.WriteLine($" -> Aktualizacja procedury: {procName}");
            // Tutaj jest łatwo: CREATE OR ALTER załatwia sprawę
            ExecuteSql(conn, script);
        }
    }

    private void UpdateTables(FbConnection conn, string scriptsDir)
    {
        string dirPath = Path.Combine(scriptsDir, FolderTables);
        if (!Directory.Exists(dirPath)) return;

        var files = Directory.GetFiles(dirPath, "*.sql");
        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string tableName = Path.GetFileNameWithoutExtension(file);

            if (!ObjectExists(conn, tableName, "RDB$RELATIONS"))
            {
                // Tabela nie istnieje -> Tworzymy całą
                Console.WriteLine($" -> Tworzenie nowej tabeli: {tableName}");
                ExecuteSql(conn, script);
            }
            else
            {
                // Tabela istnieje -> Sprawdzamy brakujące kolumny
                UpdateTableColumns(conn, tableName, script);
            }
        }
    }

    private void UpdateTableColumns(FbConnection conn, string tableName, string script)
    {
        // 1. Parsujemy skrypt, żeby wyciągnąć definicje kolumn
        // Szukamy linii w stylu: "   COLUMN_NAME TYPE..."
        var columnsInScript = ParseColumnsFromScript(script);

        // 2. Pobieramy istniejące kolumny z bazy
        var existingColumns = GetExistingColumns(conn, tableName);

        foreach (var col in columnsInScript)
        {
            if (!existingColumns.Contains(col.Name.ToUpper()))
            {
                Console.WriteLine($"    -> [UPDATE] Dodawanie kolumny {col.Name} do tabeli {tableName}");

                // Budujemy ALTER TABLE
                // Uwaga: col.FullDefinition zawiera np. "EMAIL VARCHAR(100)"
                // My potrzebujemy: ALTER TABLE CUSTOMERS ADD EMAIL VARCHAR(100)

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
    }

    // --- Metody Pomocnicze ---

    private bool ObjectExists(FbConnection conn, string name, string sysTable)
    {
        // Uniwersalna metoda do sprawdzania czy coś istnieje w RDB$FIELDS lub RDB$RELATIONS
        string nameCol = sysTable == "RDB$RELATIONS" ? "RDB$RELATION_NAME" : "RDB$FIELD_NAME";

        // W Firebird nazwy obiektów systemowych są 'case sensitive' jeśli nie używamy cudzysłowów, 
        // ale standardowo są UPPERCASE.
        string sql = $"SELECT COUNT(*) FROM {sysTable} WHERE {nameCol} = @Name";

        // Ważne: Wykluczamy tabele systemowe (dla RDB$RELATIONS)
        if (sysTable == "RDB$RELATIONS")
        {
            sql += " AND RDB$SYSTEM_FLAG = 0";
        }

        using (var cmd = new FbCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("@Name", name.ToUpper());
            int count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }
    }

    private HashSet<string> GetExistingColumns(FbConnection conn, string tableName)
    {
        var cols = new HashSet<string>();
        string sql = "SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS WHERE RDB$RELATION_NAME = @TableName";

        using (var cmd = new FbCommand(sql, conn))
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

    private void ExecuteSql(FbConnection conn, string sql)
    {
        using (var cmd = new FbCommand(sql, conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    // Prosta klasa wewnętrzna
    private class ColumnInfo { public string Name; public string FullDefinition; }

    private List<ColumnInfo> ParseColumnsFromScript(string script)
    {
        var list = new List<ColumnInfo>();

        // Rozbijamy skrypt na linie
        var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            string trimmed = line.Trim();

            // Pomijamy linie "CREATE TABLE", nawiasy, puste itp.
            if (trimmed.StartsWith("CREATE TABLE") || trimmed == "(" || trimmed == ");" || trimmed == ")")
                continue;

            // Usuwamy ewentualny przecinek na końcu linii
            if (trimmed.EndsWith(","))
                trimmed = trimmed.Substring(0, trimmed.Length - 1);

            // Parsowanie: Pierwsze słowo to nazwa kolumny, reszta to definicja
            // Np. "FIRST_NAME VARCHAR(50)" -> Name="FIRST_NAME", Def="FIRST_NAME VARCHAR(50)"

            var parts = trimmed.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                list.Add(new ColumnInfo
                {
                    Name = parts[0],
                    FullDefinition = trimmed // Cała linia to definicja kolumny
                });
            }
        }
        return list;
    }
}