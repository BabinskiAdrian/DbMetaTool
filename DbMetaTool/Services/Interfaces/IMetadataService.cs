namespace DbMetaTool.Services.Interfaces;

public interface IMetadataService
{
    public void ExportDatabase(string connectionString, string outputDirectory);
}
