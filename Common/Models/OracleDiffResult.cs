using System.Collections.Generic;

namespace Common.Models
{
    public class OracleDiffResult
    {
        public string ViewName { get; set; }
        public string FormattedDiff { get; set; }
        public bool HasDifferences { get; set; }

        public OracleDiffResult(string viewName, string formattedDiff, bool hasDifferences)
        {
            ViewName = viewName;
            FormattedDiff = formattedDiff;
            HasDifferences = hasDifferences;
        }
    }
}