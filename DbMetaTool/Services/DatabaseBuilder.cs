using FirebirdSql.Data.FirebirdClient;
using DbMetaTool.Services.Interfaces;

namespace DbMetaTool.Services;

public class DatabaseBuilder : IDatabaseBuilder
{
    // Nazwy folderów
    private const string FolderDomains = "Domains";
    private const string FolderTables = "Tables";
    private const string FolderProcedures = "Procedures";

    // Rozszerzenia plików
    private const string SqlExtension = "*.sql";

    // Konfiguracja bazy (Domyślne poświadczenia)
    private const string DbUser = "SYSDBA";
    private const string DbPassword = "senteTask1!";
    private const string DbDialect = "3";
    private const string DbCharset = "UTF8";

    public void BuildDatabase(string databasePath, string scriptsDirectory)
    {
        Console.WriteLine($"[INFO] Rozpoczynam budowanie bazy: {databasePath}");

        // Tworzenie pustego pliku bazy danych
        CreateEmptyDatabase(databasePath);

        string connectionString = BuildConnectionString(databasePath);

        using (var conn = new FbConnection(connectionString))
        {
            conn.Open();

            // Wykonywanie skryptów w odpowiedniej kolejności
            RunScriptsFromFolder(conn, scriptsDirectory, FolderDomains);
            RunScriptsFromFolder(conn, scriptsDirectory, FolderTables);
            RunScriptsFromFolder(conn, scriptsDirectory, FolderProcedures);
        }

        Console.WriteLine("[SUKCES] Baza została zbudowana pomyślnie.");
    }

    private void CreateEmptyDatabase(string databasePath)
    {
        if (File.Exists(databasePath))
        {
            Console.WriteLine($"[WARN] Usuwanie starej bazy: {databasePath}");
            try
            {
                File.Delete(databasePath);
            }
            catch (IOException)
            {
                Console.WriteLine("[BŁĄD] Nie można usunąć pliku. Upewnij się, że nie jest otwarty w IBExpert!");
                throw;
            }
        }

        Console.WriteLine(" -> Tworzenie pliku .fdb...");

        string createCs = BuildConnectionString(databasePath);
        FbConnection.CreateDatabase(createCs);
    }

    private static void RunScriptsFromFolder(FbConnection conn, string baseDir, string folderName)
    {
        string dirPath = Path.Combine(baseDir, folderName);

        if (!Directory.Exists(dirPath))
        {
            Console.WriteLine($"[INFO] Brak folderu {folderName}, pomijam.");
            return;
        }

        Console.WriteLine($" -> Wykonywanie skryptów z: {folderName}...");

        var files = Directory.GetFiles(dirPath, SqlExtension);

        foreach (var file in files)
        {
            string script = File.ReadAllText(file);
            string fileName = Path.GetFileName(file);

            Console.WriteLine($"    -> Wykonywanie: {fileName}");

            try
            {
                using (var cmd = new FbCommand(script, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    [BŁĄD] Błąd w pliku {fileName}: {ex.Message}");
                Console.ResetColor();
                throw;
            }
        }
    }

    private string BuildConnectionString(string databasePath)
    {
        var csBuilder = new FbConnectionStringBuilder
        {
            UserID = DbUser,
            Password = DbPassword,
            Database = $"localhost:{databasePath}", // Firebird wymaga localhost dla TCP/IP
            Dialect = int.Parse(DbDialect),
            Charset = DbCharset,
            ServerType = FbServerType.Default // Lub Embedded, zależnie od wersji
        };

        return csBuilder.ToString();
    }
}