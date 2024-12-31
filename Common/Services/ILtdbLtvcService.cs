using Common.Models;

namespace Common.Services
{
    public interface ILtdbLtvcService
    {
        Task<(List<LtdbLtvcRecord> Results, int TotalCount)> ExecuteQueryAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            double? minDelay = null,
            int pageSize = 10,
            int pageNumber = 1,
            CancellationToken cancellationToken = default);

        string BuildQuery(
            DateTimeOffset? startDate,
            DateTimeOffset? endDate,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            double? minDelay = null,
            int pageSize = 10,
            int pageNumber = 1);

        Task<List<(DateTime Timestamp, double AvgDelaySeconds, double MaxDelaySeconds, int RequestCount)>> GetDelayMetricsAsync(
            string environment,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null,
            string? containerNumber = null,
            string? placa = null,
            string? motorista = null,
            string? moveType = null,
            long? idAgendamento = null,
            string? status = null,
            CancellationToken cancellationToken = default);
    }
}
