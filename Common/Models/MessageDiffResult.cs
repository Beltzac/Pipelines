namespace Common.Models
{
    public class MessageDiffResult : IDiffResult
    {
        public string Key { get; set; }
        public bool HasDifferences { get; set; }
        public string FormattedDiff { get; set; }
        public MessageDefinition Target { get; set; }
        public MessageDefinition Source { get; set; }
        public List<string> ChangedFields { get; set; } = new();
    }
}