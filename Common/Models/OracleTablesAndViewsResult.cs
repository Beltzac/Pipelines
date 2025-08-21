using System.Collections.Generic;

namespace Common.Models
{
    public class OracleTablesAndViewsResult
    {
        public IEnumerable<OracleTableOrViewInfo> Results { get; set; } = new List<OracleTableOrViewInfo>();
        public int TotalCount { get; set; }
    }
}