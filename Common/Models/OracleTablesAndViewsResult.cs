using System.Collections.Generic;

namespace Common.Models
{
    public class OracleTablesAndViewsResult
    {
        public IEnumerable<string> Results { get; set; } = new List<string>();
        public int TotalCount { get; set; }
    }
}