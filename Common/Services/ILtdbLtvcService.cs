using Common.Models;

namespace Common.Services
{
    public interface ILtdbLtvcService
    {
        Task<(List<LtdbLtvcRecord> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            int pageSize = 10,
            int pageNumber = 1,
            CancellationToken cancellationToken = default);

        string BuildQuery(
            DateTime? startDate,
            DateTime? endDate,
            string? containerNumber,
            string? placa,
            string? motorista,
            string? moveType,
            long? idAgendamento,
            string? status,
            int pageSize,
            int pageNumber);
    }
}
