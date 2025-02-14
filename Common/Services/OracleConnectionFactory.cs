using Common.Services.Interfaces;
using Oracle.ManagedDataAccess.Client;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Common.Models;

namespace Common.Services
{
    public class OracleConnectionFactory : IOracleConnectionFactory
    {
        public DbContext CreateContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TcpDbContext>();
            optionsBuilder.UseOracle(connectionString, options => {
            
            }).UseUpperSnakeCaseNamingConvention();

            return new TcpDbContext(optionsBuilder.Options);
        }
    }

    public class TcpDbContext : DbContext
    {
        public virtual DbSet<OracleViewDefinition> OracleViewDefinition { get; set; }

        public TcpDbContext(DbContextOptions<TcpDbContext> options): base(options)
        {
        }
    }
}