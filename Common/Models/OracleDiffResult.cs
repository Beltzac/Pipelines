using System.Collections.Generic;

namespace Common.Models
{
    public class OracleDiffResult: IDiffResult
    {
        public string Key { get; set; }
        public string FormattedDiff { get; set; }
        public bool HasDifferences { get; set; }

        public OracleDiffResult(string viewName, string formattedDiff, bool hasDifferences)
        {
            Key = viewName;
            FormattedDiff = formattedDiff;
            HasDifferences = hasDifferences;
        }
    }
}