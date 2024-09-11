using System;
using LibGit2Sharp;

namespace Common
{
    public interface IRepositoryCloner
    {
        void Clone(string sourceUrl, string workdirPath, CloneOptions options);
    }
}
