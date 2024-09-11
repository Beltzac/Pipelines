using LiteDB.Async;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public class LiteDbRepositoryDatabase : IRepositoryDatabase
    {
        private readonly ILiteDatabaseAsync _liteDatabase;
        private readonly ILiteCollectionAsync<Repository> _reposCollection;

        public LiteDbRepositoryDatabase(ILiteDatabaseAsync liteDatabase)
        {
            _liteDatabase = liteDatabase;
            _reposCollection = _liteDatabase.GetCollection<Repository>("repos");
        }

        public Task<Repository> FindByIdAsync(Guid id)
        {
            return _reposCollection.FindByIdAsync(id);
        }

        public async Task<List<Repository>> FindAllAsync()
        {
            var result = await _reposCollection.FindAllAsync();
            return result.ToList();
        }

        public Task UpsertAsync(Repository repository)
        {
            return _reposCollection.UpsertAsync(repository);
        }

        public Task DeleteAsync(Guid id)
        {
            return _reposCollection.DeleteAsync(id);
        }

        public ILiteQueryableAsync<Repository> Query()
        {
            return _reposCollection.Query();
        }
    }
}
