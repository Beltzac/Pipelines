using Common.Repositories;
using Common.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public class RepositoryDbContextFactory : IDesignTimeDbContextFactory<RepositoryDbContext>
{
    public RepositoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RepositoryDbContext>();
        var databasePath = DBUtils.MainDBPath;
        var connectionString = $"Data Source={databasePath}";
        optionsBuilder.UseSqlite(connectionString)
                      .EnableSensitiveDataLogging();

        return new RepositoryDbContext(optionsBuilder.Options);
    }
}
