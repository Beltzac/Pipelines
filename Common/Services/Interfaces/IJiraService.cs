using System.Threading.Tasks;

namespace Common.Services.Interfaces
{
    public interface IJiraService
    {
        Task<string> GetIssueIdByKeyAsync(string issueKey);
        Task<bool> TestConnectionAsync();
    }
}