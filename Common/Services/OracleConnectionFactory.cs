using Common.Models;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Common.Services
{
    public class OracleConnectionFactory : IOracleConnectionFactory
    {
        public DbContext CreateContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TcpDbContext>();
            optionsBuilder.UseOracle(connectionString)
                          .EnableServiceProviderCaching(false);

            return new TcpDbContext(optionsBuilder.Options);
        }
    }

    public class TcpDbContext : DbContext
    {
        public virtual DbSet<OracleViewDefinition> OracleViewDefinition { get; set; }

        public TcpDbContext(DbContextOptions<TcpDbContext> options) : base(options)
        {
        }
    }
}