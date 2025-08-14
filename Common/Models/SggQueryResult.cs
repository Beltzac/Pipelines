using System.Collections.Generic;

namespace Common.Models
{
    public class SggQueryResult
    {
        public List<LtdbLtvcRecord> Results { get; set; }
        public int TotalCount { get; set; }
    }
}