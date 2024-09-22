using Microsoft.EntityFrameworkCore;

namespace Common
{
    public class RepositoryDbContext : DbContext
    {
        public RepositoryDbContext(DbContextOptions<RepositoryDbContext> options)
            : base(options)
        {
        }

        public RepositoryDbContext()
        {
        }

        public DbSet<Repository> Repositories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configure the database provider and connection string
                var databasePath = @"C:\repos\Builds.db";
                var connectionString = $"Data Source={databasePath}";
                optionsBuilder.UseSqlite(connectionString);
            }
        }
    }
}
