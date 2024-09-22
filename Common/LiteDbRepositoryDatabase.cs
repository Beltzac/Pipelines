using Microsoft.Data.Sqlite;
using System.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Common
{
    public class SqliteRepositoryDatabase : IRepositoryDatabase
    {
        private readonly string _connectionString;

        public SqliteRepositoryDatabase(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<Repository> FindByIdAsync(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM repos WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Repository
                {
                    Id = reader.GetGuid(0),
                    // Map other fields
                };
            }

            return null;
        }

        public async Task<List<Repository>> FindAllAsync()
        {
            var repositories = new List<Repository>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM repos";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                repositories.Add(new Repository
                {
                    Id = reader.GetGuid(0),
                    // Map other fields
                });
            }

            return repositories;
        }

        public async Task UpsertAsync(Repository repository)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO repos (Id, /* other fields */) 
                VALUES ($id, /* other values */)
                ON CONFLICT(Id) DO UPDATE SET /* field updates */;
            ";
            command.Parameters.AddWithValue("$id", repository.Id);
            // Add other parameters

            await command.ExecuteNonQueryAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM repos WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);

            await command.ExecuteNonQueryAsync();
        }

        public IQueryable<Repository> Query()
        {
            throw new NotImplementedException("Query method is not implemented for SQLite.");
        }
    }
}
