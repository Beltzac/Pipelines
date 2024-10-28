using Common.Models;

namespace Common.Services
{
    public interface IRequisicaoExecucaoService
    {
        Task<List<RequisicaoExecucao>> ExecuteQueryAsync(string environment, DateTime? startDate = null, string? urlFilter = null, string? httpMethod = null, string[]? containerNumbers = null, string? nomeFluxo = null, int? userId = null, int? execucaoId = null, int maxRows = 10, string? httpStatusRange = null, string? responseStatus = null);
        Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null);
    }
}
