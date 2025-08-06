using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IEsbService
    {
        Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string? genericText = null,
            long? userId = null,
            int? execucaoId = null,
            int pageSize = 10,
            int pageNumber = 1,
            string? httpStatusRange = null,
            string? responseStatus = null,
            int? minDelaySeconds = null,
            CancellationToken cancellationToken = default);

        string BuildQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? urlFilter,
            string? httpMethod,
            string? genericText,
            long? userId,
            int? execucaoId,
            int pageSize,
            int pageNumber,
            string? httpStatusRange,
            string? responseStatus,
            int? minDelaySeconds);

        Task<string> GetEsbSequencesAsync(string soapRequest, EsbServerConfig esbServer);
        Task<List<SequenceInfo>> GetSequencesAsync(EsbServerConfig esbServer);
    }
}
