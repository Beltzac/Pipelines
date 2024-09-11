using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public interface IRepositoryDatabase
    {
        Task<Repository> FindByIdAsync(Guid id);
        Task<List<Repository>> FindAllAsync();
        Task UpsertAsync(Repository repository);
        Task DeleteAsync(Guid id);
        ILiteQueryableAsync<Repository> Query();
    }
}
