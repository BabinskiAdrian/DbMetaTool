using System;
using DbMetaTool.Services.Exporters;
using DbMetaTool.Services.Interfaces;
using FirebirdSql.Data.FirebirdClient;

namespace DbMetaTool.Services
{
    public class MetadataService : IMetadataService
    {
        // Instancje eksporterów (w prawdziwym projekcie wstrzykiwane przez DI, tu wystarczy new)
        private readonly DomainExporter _domainExporter = new DomainExporter();
        private readonly TableExporter _tableExporter = new TableExporter();
        private readonly ProcedureExporter _procedureExporter = new ProcedureExporter();

        public void ExportDatabase(string connectionString, string outputDirectory)
        {
            Console.WriteLine($"[INFO] Łączenie z bazą: {connectionString}");

            using (var conn = new FbConnection(connectionString))
            {
                conn.Open();

                // 1. Domeny
                _domainExporter.Export(conn, outputDirectory);

                // 2. Tabele
                _tableExporter.Export(conn, outputDirectory);

                // 3. Procedury
                _procedureExporter.Export(conn, outputDirectory);
            }
            Console.WriteLine("[SUKCES] Eksport zakończony.");
        }
    }
}