namespace Common.Models
{
    public class OracleViewDefinition
    {
        public string Name { get; set; }
        public string Definition { get; set; }

        public OracleViewDefinition(string name, string definition)
        {
            Name = name;
            Definition = definition;
        }
    }
}