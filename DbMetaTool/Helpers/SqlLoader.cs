using System.Reflection;

namespace DbMetaTool.Helpers;

public static class SqlResourceLoader
{
    private const string SqlFolder = "SqlQueries";

    public static string Load(string queryName)
    {
        var assembly = Assembly.GetExecutingAssembly();

        string assemblyName = assembly.GetName().Name ?? string.Empty;
        string resourcePath = $"{assemblyName}.{SqlFolder}.{queryName}";

        using (var stream = assembly.GetManifestResourceStream(resourcePath))
        {
            if (stream == null)
            {
                var availableResources = string.Join("\n", assembly.GetManifestResourceNames());

                throw new FileNotFoundException(
                    $"Nie znaleziono zasobu wbudowanego: '{resourcePath}'.\n" +
                    $"Dostępne zasoby:\n{availableResources}");
            }

            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}