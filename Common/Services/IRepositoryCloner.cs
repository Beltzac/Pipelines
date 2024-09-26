using LibGit2Sharp;

namespace Common.Services
{
    public interface IRepositoryCloner
    {
        void Clone(string sourceUrl, string workdirPath, CloneOptions options);
    }
}
