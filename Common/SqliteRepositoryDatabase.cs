using Microsoft.EntityFrameworkCore;

namespace Common
{
    public class SqliteRepositoryDatabase : IRepositoryDatabase
    {
        private readonly IDbContextFactory<RepositoryDbContext> _contextFactory;

        public SqliteRepositoryDatabase(IDbContextFactory<RepositoryDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<Repository> FindByIdAsync(Guid id)
        {
            await using var context = _contextFactory.CreateDbContext();
            return await context.Repositories.FindAsync(id);
        }

        public async Task<List<Repository>> FindAllAsync()
        {
            await using var context = _contextFactory.CreateDbContext();
            return await context.Repositories.ToListAsync();
        }

        public async Task UpsertAsync(Repository repository)
        {
            await using var context = _contextFactory.CreateDbContext();
            var existing = await context.Repositories.FindAsync(repository.Id);
            if (existing == null)
            {
                context.Repositories.Add(repository);
            }
            else
            {
                context.Entry(existing).CurrentValues.SetValues(repository);
            }
            await context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await using var context = _contextFactory.CreateDbContext();
            var repository = await context.Repositories.FindAsync(id);
            if (repository != null)
            {
                context.Repositories.Remove(repository);
                await context.SaveChangesAsync();
            }
        }

        public IQueryable<Repository> Query()
        {
            var context = _contextFactory.CreateDbContext();
            return context.Repositories.AsQueryable();
        }
    }
}
