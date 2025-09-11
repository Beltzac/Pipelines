namespace Common.Models
{
    public class OracleDependency
    {
        public string Type { get; set; }
        public string Referencee { get; set; }
        public string ReferencedSchema { get; set; }
        public string ReferencedName { get; set; }
    }
}