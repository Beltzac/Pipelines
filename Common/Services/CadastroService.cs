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
                SELECT
	                U.ID_USUARIO,
	                L.LOGIN,
	                U.NOME 
                FROM
	                TCPCAD.LOGIN L
	                INNER JOIN TCPCAD.USUARIO U ON L.ID_USUARIO = U.ID_USUARIO
                WHERE UPPER(LOGIN) LIKE '%' || UPPER({searchText ?? string.Empty}) || '%' OR UPPER(NOME) LIKE '%' || UPPER({searchText ?? string.Empty}) || '%'
                FETCH FIRST 10 ROWS ONLY",
                default);
        }
    }
}
