namespace Common.Models
{
    public class SavedQuery
    {
        public string Name { get; set; }
        public string QueryString { get; set; }
        public string QueryType { get; set; } // "SQL" or "MongoDB"
        public string Description { get; set; }
    }
}