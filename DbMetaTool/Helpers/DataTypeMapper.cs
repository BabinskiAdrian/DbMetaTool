namespace DbMetaTool.Helpers;

public static class DataTypeMapper
{
    public static string Map(short type, short charLen, short scale, string domainName)
    {
        // 1. Jeśli mamy nazwę domeny użytkownika, używamy jej
        if (!string.IsNullOrEmpty(domainName)
            && !domainName.StartsWith("RDB$")
            && domainName != "RDB$FORCE_RAW")
        {
            return domainName;
        }

        // 2. Obsługa typów numerycznych ze skalą (np. waluty)
        if (scale < 0)
        {
            return "NUMERIC(15,2)";
        }

        // 3. Mapowanie typów prostych
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