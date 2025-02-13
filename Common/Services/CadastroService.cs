using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;

namespace Common.Services
{
    public class CadastroService : ICadastroService
    {
        private readonly IOracleRepository _repo;

        public CadastroService(IOracleRepository repo)
        {
            _repo = repo;
        }

        public async Task<List<Usuario>> GetUsersAsync(string environment, string? searchText = null)
        {
            return await _repo.GetFromSqlAsync<Usuario>(
                environment,
                @$"
                SELECT id_usuario, login
                FROM TCPCAD.login
                WHERE UPPER(login) LIKE '%' || UPPER({searchText ?? string.Empty}) || '%'
                FETCH FIRST 10 ROWS ONLY",
                default);
        }
    }
}
