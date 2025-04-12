using Common.Models;

namespace Common.Services.Interfaces
{
    public interface ISggService
    {
        Task<(List<LtdbLtvcRecord> Results, int TotalCount)> ExecuteQueryAsync(
            SggQueryFilter filter,
            CancellationToken cancellationToken = default);

        string BuildQuery(SggQueryFilter filter);

        Task<List<DelayMetric>> GetDelayMetricsAsync(
            SggQueryFilter filter,
            CancellationToken cancellationToken = default);
    }
}
