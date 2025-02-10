using Common.Services.Interfaces;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace Common.Services
{
    public class OracleConnectionFactory : IOracleConnectionFactory
    {
        public IDbConnection CreateConnection(string connectionString)
        {
            return new OracleConnection(connectionString);
        }
    }
}