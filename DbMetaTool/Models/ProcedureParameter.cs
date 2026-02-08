namespace DbMetaTool.Models
{
    public class ProcedureParameter
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public short ParameterType { get; set; } // 0 = INPUT, 1 = OUTPUT

        public override string ToString()
        {
            return $"    {Name} {DataType}";
        }
    }
}