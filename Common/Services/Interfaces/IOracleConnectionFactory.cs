using Microsoft.EntityFrameworkCore;
using Common.Models;

namespace Common.Services.Interfaces
{
    public interface IOracleConnectionFactory
    {
        DbContext CreateContext(string connectionString);
    }
}
