using Common.Services.Interfaces;
using Dapper;
using Oracle.ManagedDataAccess.Client;

namespace Common.Services
{
    public class CadastroService : ICadastroService
    {
        private readonly IConfigurationService _configService;

        public CadastroService(IConfigurationService configService)
        {
            _configService = configService;
        }

        public async Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null)
        {
            var config = _configService.GetConfig();
            var oracleEnv = config.OracleEnvironments.FirstOrDefault(x => x.Name == environment)
                ?? throw new ArgumentException($"Environment {environment} not found");

            using var connection = new OracleConnection(oracleEnv.ConnectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT id_usuario, login
                FROM TCPCAD.login
                WHERE UPPER(login) LIKE '%' || UPPER(:searchText) || '%'
                FETCH FIRST 10 ROWS ONLY";

            var results = await connection.QueryAsync<(int id_usuario, string login)>(
                sql,
                new { searchText = searchText ?? string.Empty });

            return results.ToDictionary(x => x.id_usuario, x => x.login);
        }
    }
}
