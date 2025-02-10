using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Common.Services.Interfaces
{
    public interface IOracleConnectionFactory
    {
        IDbConnection CreateConnection(string connectionString);
    }
}