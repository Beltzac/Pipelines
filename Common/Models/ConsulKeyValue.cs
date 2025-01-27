namespace Common.Models
{
    public class ConsulKeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string ValueRecursive { get; set; }
        public bool IsValidJson { get; set; }
        public string Url { get; set; }
    }
}
