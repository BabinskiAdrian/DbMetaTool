namespace DbMetaTool.Models
{
    public class ColumnDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsNullable { get; set; }

        public override string ToString()
        {
            string nullPart = IsNullable ? "" : " NOT NULL";
            return $"    {Name} {DataType}{nullPart}";
        }
    }
}