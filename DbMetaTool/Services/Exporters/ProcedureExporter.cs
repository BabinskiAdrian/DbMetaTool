using System.Text;
using DbMetaTool.Helpers;
using DbMetaTool.Models;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Services.Exporters;

public class ProcedureExporter
{
    private const string GetAllProceduresQueryFile = "GetAllProcedures.sql";
    private const string GetProcedureParametersQueryFile = "GetProcedureParameters.sql";
    private const string ProceduresFolderName = "Procedures";

    public void Export(FbConnection conn, string outputDir)
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
                string procName = reader["PROC_NAME"]?.ToString() ?? string.Empty;
                string procSource = reader["PROC_SOURCE"]?.ToString() ?? string.Empty;

                // Obsługa złożonej logiki
                ExportSingleProcedure(conn, procDir, procName, procSource);
            }
        }
    }

    private void ExportSingleProcedure(FbConnection conn, string procDir, string procName, string procSource)
    {
        Console.WriteLine($"    -> Przetwarzanie procedury: {procName}");

        // 1. Pobranie parametrów
        var parameters = GetParametersForProcedure(conn, procName);

        var inputParams = parameters.Where(p => p.ParameterType == 0).ToList();
        var outputParams = parameters.Where(p => p.ParameterType == 1).ToList();

        // 2. Budowanie treści skryptu SQL
        var sb = new StringBuilder();
        sb.Append($"CREATE OR ALTER PROCEDURE {procName}");

        // Parametry wejściowe
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

        // Parametry wyjściowe (RETURNS)
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

        // 3. Zapis do pliku
        File.WriteAllText(Path.Combine(procDir, $"{procName}.sql"), sb.ToString());
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
                        Name = reader["PARAM_NAME"].ToString() ?? string.Empty,
                        ParameterType = Convert.ToInt16(reader["PARAM_TYPE"])
                    };

                    short fieldType = Convert.ToInt16(reader["FIELD_TYPE"]);
                    short charLen = reader["DATA_CHAR_LENGTH"] != DBNull.Value ? Convert.ToInt16(reader["DATA_CHAR_LENGTH"]) : (short)0;
                    short scale = Convert.ToInt16(reader["FIELD_SCALE"]);
                    string domainName = reader["DOMAIN_NAME"].ToString() ?? string.Empty;

                    // Użycie wspólnego helpera
                    param.DataType = DataTypeMapper.Map(fieldType, charLen, scale, domainName);

                    parameters.Add(param);
                }
            }
        }
        return parameters;
    }
}