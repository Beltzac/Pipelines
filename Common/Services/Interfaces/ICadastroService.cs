using Common.Models;

namespace Common.Services.Interfaces
{
    public interface ICadastroService
    {
        Task<List<Usuario>> GetUsersAsync(string environment, string? searchText = null);
    }
}