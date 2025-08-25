namespace Common.Repositories.TCP.Interfaces
{
    public interface IOracleRepository
    {
        Task<List<T>> GetFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken);
        Task<T> GetSingleFromSqlAsync<T>(string connectionString, FormattableString sql, CancellationToken cancellationToken);
        Task<List<Dictionary<string, object>>> GetFromSqlDynamicAsync(string connectionString, FormattableString sql, CancellationToken cancellationToken);
    }
}
