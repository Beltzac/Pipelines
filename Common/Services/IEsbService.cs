using Common.Models;

namespace Common.Services
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
            int? userId = null,
            int? execucaoId = null,
            int pageSize = 10,
            int pageNumber = 1,
            string? httpStatusRange = null,
            string? responseStatus = null,
            CancellationToken cancellationToken = default);

        string BuildQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? urlFilter,
            string? httpMethod,
            string? genericText,
            int? userId,
            int? execucaoId,
            int pageSize,
            int pageNumber,
            string? httpStatusRange,
            string? responseStatus);

        Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null);
    }
}
