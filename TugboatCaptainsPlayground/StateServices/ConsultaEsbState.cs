using Common.Models;
using Common.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class ConsultaEsbState : ITracksLoading, IPaginates
    {
        public string SelectedEnvironment { get; set; }
        public DateTimeOffset? StartDate { get; set; }
        public DateTimeOffset? EndDate { get; set; }
        public Usuario? User { get; set; }
        public RequisicaoExecucao SelectedItem { get; set; }
        public string UrlFilter { get; set; }
        public string HttpMethod { get; set; }
        public string GenericText { get; set; }
        public int? ExecucaoId { get; set; }
        public string HttpStatusRange { get; set; }
        public string ResponseStatus { get; set; }
        public int? MinDelaySeconds { get; set; }
        public List<RequisicaoExecucao> Results { get; set; } = new();

        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }

        public string FormattedRequest { get; set; }
        public string FormattedResponse { get; set; }
        public string FormattedError { get; set; }

        public bool IsLoading { get; set; }
        public int? ProgressValue { get; set; }
        public string ProgressLabel { get; set; } = string.Empty;
    }
}
