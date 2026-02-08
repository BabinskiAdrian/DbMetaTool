using System.Text;
using DbMetaTool.Helpers;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Services.Exporters;

public class DomainExporter
{
    private readonly string GetAllDomainsQueryFile = "GetAllDomains.sql";
    private readonly string DomainsFolderName = "Domains";

    public void Export(FbConnection conn, string outputDir)
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
                string domainName = reader["DOMAIN_NAME"].ToString() ?? string.Empty;
                Console.WriteLine($"    -> Przetwarzanie domeny: {domainName}");

                short fieldType = Convert.ToInt16(reader["FIELD_TYPE"]);
                short charLen = reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0;
                short scale = Convert.ToInt16(reader["FIELD_SCALE"]);

                // Używamy helpera z flagą RDB$FORCE_RAW, aby uzyskać typ bazowy (np. INTEGER)
                string dataType = DataTypeMapper.Map(fieldType, charLen, scale, "RDB$FORCE_RAW");

                string defaultSource = reader["DEFAULT_SOURCE"]?.ToString() ?? string.Empty;
                bool isNotNull = reader["NULL_FLAG"] != DBNull.Value && Convert.ToInt32(reader["NULL_FLAG"]) == 1;

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
}