using Common.Models;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Common.Services
{
    public class OracleConnectionFactory : IOracleConnectionFactory
    {
        private readonly ILogger<OracleConnectionFactory> _logger;

        public OracleConnectionFactory(ILogger<OracleConnectionFactory> logger)
        {
            _logger = logger;
        }

        public DbContext CreateContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TcpDbContext>();
            optionsBuilder
                .UseOracle(connectionString, options => { })
                .UseUpperSnakeCaseNamingConvention()
                .EnableSensitiveDataLogging()
                .LogTo(msg => _logger.LogInformation(msg), LogLevel.Information);

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