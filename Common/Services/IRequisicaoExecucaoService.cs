using Common.Models;

namespace Common.Services
{
    public interface IRequisicaoExecucaoService
    {
        Task<(List<RequisicaoExecucao> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? urlFilter = null,
            string? httpMethod = null,
            string[]? containerNumbers = null,
            string? nomeFluxo = null,
            int? userId = null,
            int? execucaoId = null,
            int pageSize = 10,
            int pageNumber = 1,
            string? httpStatusRange = null,
            string? responseStatus = null,
            CancellationToken cancellationToken = default);
            
        Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null);
    }
}
