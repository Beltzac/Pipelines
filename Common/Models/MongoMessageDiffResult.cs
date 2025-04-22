using CSharpDiff.Patches.Models;

namespace Common.Models
{
    public class MongoMessageDiffResult : IDiffResult
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string Key { get; set; }
        public string Idioma { get; set; }
        public MongoMessage Source { get; set; }
        public MongoMessage Target { get; set; }
        public List<string> ChangedFields { get; set; } = new List<string>();
        public string FormattedDiff { get; set; }
        public bool HasDifferences { get; set; }
        public PatchResult Patch { get; set; }
    }
}