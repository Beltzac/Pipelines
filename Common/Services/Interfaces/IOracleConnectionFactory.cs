using Microsoft.EntityFrameworkCore;

namespace Common.Services.Interfaces
{
    public interface IOracleConnectionFactory
    {
        DbContext CreateContext(string connectionString);
    }
}
