using System.Text;
using DbMetaTool.Helpers;
using DbMetaTool.Models;
using DbMetaTool.Services.Interfaces;
using FirebirdSql.Data.FirebirdClient;
using System.Linq;

namespace DbMetaTool.Services
{
    public class MetadataService : IMetadataService
    {
        // Stałe konfiguracyjne
        private const string GetAllTablesQueryFile = "GetAllTables.sql";
        private const string GetColumnsQueryFile = "GetTableColumns.sql";
        private const string TablesFolderName = "Tables";
        private const string GetAllProceduresQueryFile = "GetAllProcedures.sql";
        private const string ProceduresFolderName = "Procedures";
        private const string GetProcedureParametersQueryFile = "GetProcedureParameters.sql";
        private const string GetAllDomainsQueryFile = "GetAllDomains.sql";
        private const string DomainsFolderName = "Domains";

        // Główna metoda uruchamiająca eksport
        public void ExportDatabase(string connectionString, string outputDir)
        {
            Console.WriteLine($"[INFO] Łączenie z bazą: {connectionString}");

            using (var conn = new FbConnection(connectionString))
            {
                conn.Open();
                
                // 1. Domeny
                ExportDomains(conn, outputDir);

                // 2. Tabele
                ExportTables(conn, outputDir);

                // 3. Procedury
                ExportProcedures(conn, outputDir);
            }
            Console.WriteLine("[SUKCES] Eksport zakończony.");
        }

        private void ExportProcedures(FbConnection conn, string outputDir)
        {
            Console.WriteLine(" -> Pobieranie definicji procedur...");
            string procDir = Path.Combine(outputDir, ProceduresFolderName);
            Directory.CreateDirectory(procDir);

            string sql = SqlResourceLoader.Load(GetAllProceduresQueryFile);

            using (var cmd = new FbCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string procName = reader["PROC_NAME"].ToString();
                    Console.WriteLine($"    -> Przetwarzanie procedury: {procName}");

                    string procSource = reader["PROC_SOURCE"] != DBNull.Value
                        ? reader["PROC_SOURCE"].ToString()
                        : "";

                    // 1. Pobierz parametry
                    var parameters = GetParametersForProcedure(conn, procName);

                    // Rozdzielamy na wejściowe (Type 0) i wyjściowe (Type 1)
                    var inputParams = parameters.Where(p => p.ParameterType == 0).ToList();
                    var outputParams = parameters.Where(p => p.ParameterType == 1).ToList();

                    var sb = new StringBuilder();
                    sb.Append($"CREATE OR ALTER PROCEDURE {procName}");

                    // 2. Dodaj parametry wejściowe (jeśli są)
                    if (inputParams.Count > 0)
                    {
                        sb.AppendLine(" (");
                        for (int i = 0; i < inputParams.Count; i++)
                        {
                            sb.Append(inputParams[i].ToString());
                            if (i < inputParams.Count - 1) sb.Append(",");
                            sb.AppendLine();
                        }
                        sb.Append(")");
                    }

                    // 3. Dodaj parametry wyjściowe (RETURNS), jeśli są
                    if (outputParams.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("RETURNS (");
                        for (int i = 0; i < outputParams.Count; i++)
                        {
                            sb.Append(outputParams[i].ToString());
                            if (i < outputParams.Count - 1) sb.Append(",");
                            sb.AppendLine();
                        }
                        sb.Append(")");
                    }

                    sb.AppendLine();
                    sb.AppendLine("AS");
                    sb.AppendLine(procSource);

                    File.WriteAllText(Path.Combine(procDir, $"{procName}.sql"), sb.ToString());
                }
            }
        }
        private void ExportDomains(FbConnection conn, string outputDir)
        {
            Console.WriteLine(" -> Pobieranie definicji domen...");
            string domainsDir = Path.Combine(outputDir, DomainsFolderName);
            Directory.CreateDirectory(domainsDir);

            string sql = SqlResourceLoader.Load(GetAllDomainsQueryFile);

            using (var cmd = new FbCommand(sql, conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string domainName = reader["DOMAIN_NAME"].ToString();
                    Console.WriteLine($"    -> Przetwarzanie domeny: {domainName}");

                    // Pobieramy surowe dane o typie (tak samo jak w tabelach)
                    short fieldType = Convert.ToInt16(reader["FIELD_TYPE"]);
                    short fieldLen = Convert.ToInt16(reader["FIELD_LENGTH"]);
                    short charLen = reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0;
                    short scale = Convert.ToInt16(reader["FIELD_SCALE"]);

                    // UWAGA: Tu przekazujemy "RDB$" jako domainName, żeby wymusić zwrócenie typu surowego (np. INTEGER),
                    // a nie nazwy domeny (bo właśnie ją tworzymy! Nie chcemy rekurencji).
                    string dataType = MapDataType(fieldType, fieldLen, charLen, scale, "RDB$FORCE_RAW");

                    string defaultSource = reader["DEFAULT_SOURCE"] != DBNull.Value ? reader["DEFAULT_SOURCE"].ToString() : "";
                    bool isNotNull = reader["NULL_FLAG"] != DBNull.Value && Convert.ToInt32(reader["NULL_FLAG"]) == 1;

                    // Budujemy SQL: CREATE DOMAIN NAZWA AS TYP [DEFAULT ...] [NOT NULL]
                    var sb = new StringBuilder();
                    sb.Append($"CREATE DOMAIN {domainName} AS {dataType}");

                    if (!string.IsNullOrEmpty(defaultSource))
                    {
                        sb.Append($" {defaultSource}");
                    }

                    if (isNotNull)
                    {
                        sb.Append(" NOT NULL");
                    }

                    sb.Append(";");

                    File.WriteAllText(Path.Combine(domainsDir, $"{domainName}.sql"), sb.ToString());
                }
            }
        }
        private void ExportTables(FbConnection conn, string outputDir)
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

                    // 1. Pobierz kolumny dla tej tabeli
                    var columns = GetColumnsForTable(conn, tableName);

                    // 2. Zbuduj treść pliku SQL
                    var sb = new StringBuilder();
                    sb.AppendLine($"CREATE TABLE {tableName} (");

                    // Łączymy kolumny przecinkami
                    for (int i = 0; i < columns.Count; i++)
                    {
                        sb.Append(columns[i].ToString());
                        if (i < columns.Count - 1) sb.Append(","); // Przecinek dla wszystkich poza ostatnią
                        sb.AppendLine();
                    }

                    sb.AppendLine(");");

                    // 3. Zapisz plik
                    File.WriteAllText(Path.Combine(tablesDir, $"{tableName}.sql"), sb.ToString());
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
                        var col = new ColumnDefinition
                        {
                            Name = reader["COLUMN_NAME"].ToString(),
                            IsNullable = reader["NULL_FLAG"] == DBNull.Value || Convert.ToInt32(reader["NULL_FLAG"]) == 0
                        };

                        short fieldType = Convert.ToInt16(reader["FIELD_TYPE"]);
                        short fieldLen = Convert.ToInt16(reader["FIELD_LENGTH"]);
                        // Uwaga: Tutaj musi być DATA_CHAR_LENGTH zgodnie z poprawką w SQL
                        short charLen = reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0;
                        short scale = Convert.ToInt16(reader["FIELD_SCALE"]);
                        string domainName = reader["DOMAIN_NAME"].ToString();

                        col.DataType = MapDataType(fieldType, fieldLen, charLen, scale, domainName);

                        columns.Add(col);
                    }
                }
            }
            return columns;
        }

        private List<ProcedureParameter> GetParametersForProcedure(FbConnection conn, string procName)
        {
            var parameters = new List<ProcedureParameter>();
            string sql = SqlResourceLoader.Load(GetProcedureParametersQueryFile);

            using (var cmd = new FbCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ProcName", procName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var param = new ProcedureParameter
                        {
                            Name = reader["PARAM_NAME"].ToString(),
                            ParameterType = Convert.ToInt16(reader["PARAM_TYPE"])
                        };

                        // Używamy tej samej logiki mapowania co dla tabel!
                        short fieldType = Convert.ToInt16(reader["FIELD_TYPE"]);
                        short fieldLen = Convert.ToInt16(reader["FIELD_LENGTH"]);
                        short charLen = reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0;
                        short scale = Convert.ToInt16(reader["FIELD_SCALE"]);
                        string domainName = reader["DOMAIN_NAME"].ToString();

                        param.DataType = MapDataType(fieldType, fieldLen, charLen, scale, domainName);

                        parameters.Add(param);
                    }
                }
            }
            return parameters;
        }

        // Prosty mapper typów Firebird -> SQL
        private string MapDataType(short type, short len, short charLen, short scale, string domainName)
        {
            if (
                (!string.IsNullOrEmpty(domainName)
                && !domainName.StartsWith("RDB$")
                && domainName != "RDB$FORCE_RAW")
                )
            {
                return domainName;
            }

            // 2. Jeśli nie ma domeny, sprawdzamy czy to kwota (Numeric)
            if (scale < 0)
            {
                return "NUMERIC(15,2)";
            }

            switch (type)
            {
                case 7: return "SMALLINT";
                case 8: return "INTEGER";
                case 10: return "FLOAT";
                case 12: return "DATE";
                case 13: return "TIME";
                case 14: return $"CHAR({charLen})";
                case 16: return "BIGINT";
                case 27: return "DOUBLE PRECISION";
                case 35: return "TIMESTAMP";
                case 37: return $"VARCHAR({charLen})";
                case 261: return "BLOB";
                default: return "UNKNOWN";
            }
        }
    }
}