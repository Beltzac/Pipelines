using System.Collections.Generic;

namespace Common.Models
{
    public class OracleQueryResult
    {
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public int TotalCount { get; set; }
    }
}