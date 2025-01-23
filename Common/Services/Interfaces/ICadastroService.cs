namespace Common.Services.Interfaces
{
    public interface ICadastroService
    {
        Task<Dictionary<int, string>> GetUsersAsync(string environment, string? searchText = null);
    }
}