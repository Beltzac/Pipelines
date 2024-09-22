using Microsoft.EntityFrameworkCore;

namespace Common
{
    public class RepositoryDbContext : DbContext
    {
        public RepositoryDbContext(DbContextOptions<RepositoryDbContext> options)
            : base(options)
        {
        }

        public DbSet<Repository> Repositories { get; set; }
    }
}
