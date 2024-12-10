using Common.Models;

namespace Front2.Services
{
    public class ConsultaEsbState
    {
        public string SelectedEnvironment { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public KeyValuePair<int, string>? User { get; set; }
        public RequisicaoExecucao SelectedItem { get; set; }
        public string UrlFilter { get; set; }
        public string HttpMethod { get; set; }
        public string ContainerNumbers { get; set; }
        public int? ExecucaoId { get; set; }
        public string HttpStatusRange { get; set; }
        public string ResponseStatus { get; set; }
        public List<RequisicaoExecucao> Results { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public bool IsLoading { get; set; }
        public string FormattedRequest { get; set; }
        public string FormattedResponse { get; set; }
        public string FormattedError { get; set; }
    }
}
