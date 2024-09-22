using Microsoft.EntityFrameworkCore;

namespace Common
{
    public class SqliteRepositoryDatabase : IRepositoryDatabase
    {
        private readonly RepositoryDbContext _context;

        public SqliteRepositoryDatabase(RepositoryDbContext context)
        {
            _context = context;
        }

        public async Task<Repository> FindByIdAsync(Guid id)
        {
            return await _context.Repositories.FindAsync(id);
        }

        public async Task<List<Repository>> FindAllAsync()
        {
            return await _context.Repositories.ToListAsync();
        }

        public async Task UpsertAsync(Repository repository)
        {
            var existing = await _context.Repositories.FindAsync(repository.Id);
            if (existing == null)
            {
                _context.Repositories.Add(repository);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(repository);
            }
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            var repository = await _context.Repositories.FindAsync(id);
            if (repository != null)
            {
                _context.Repositories.Remove(repository);
                await _context.SaveChangesAsync();
            }
        }

        public IQueryable<Repository> Query()
        {
            return _context.Repositories.AsQueryable();
        }
    }
}
