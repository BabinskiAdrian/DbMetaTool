namespace DbMetaTool.Services.Interfaces;

public interface IDatabaseBuilder
{
    void BuildDatabase(string databasePath, string scriptsDirectory);
}
