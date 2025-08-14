using System.Collections.Generic;

namespace Common.Models
{
    public class EsbQueryResult
    {
        public List<RequisicaoExecucao> Results { get; set; }
        public int TotalCount { get; set; }
    }
}