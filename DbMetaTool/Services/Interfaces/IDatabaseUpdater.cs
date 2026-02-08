namespace DbMetaTool.Services.Interfaces;

public interface IDatabaseUpdater
{
    void UpdateDatabase(string connectionString, string scriptsDirectory);
}