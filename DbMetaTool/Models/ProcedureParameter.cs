namespace DbMetaTool.Models;

public class ProcedureParameter
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public short ParameterType { get; set; }

    public override string ToString()
    {
        return $"    {Name} {DataType}";
    }
}