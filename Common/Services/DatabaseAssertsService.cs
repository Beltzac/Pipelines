using Common.Models;
using Common.Repositories.TCP.Interfaces;
using Common.Services.Interfaces;
using Microsoft.EntityFrameworkCore; // Added for DatabaseFacade
using Common.Utils; // Added for string extension methods

namespace Common.Services
{
    public class DatabaseAssertsService
    {
        private readonly IOracleRepository _oracleRepo;
        private readonly IMongoRepository _mongoRepo;
        private readonly IOracleConnectionFactory _connectionFactory;

        public DatabaseAssertsService(IOracleRepository oracleRepo, IMongoRepository mongoRepo, IOracleConnectionFactory connectionFactory)
        {
            _oracleRepo = oracleRepo;
            _mongoRepo = mongoRepo;
            _connectionFactory = connectionFactory;
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string connectionString, SavedQuery query, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            List<Dictionary<string, object>> results = new List<Dictionary<string, object>>();

            if (query.QueryType.Equals("SQL", StringComparison.OrdinalIgnoreCase))
            {
                // Execute SQL query using OracleRepository
                // Need to find a way to get dynamic results (List<Dictionary<string, object>>)
                // as GetFromSqlAsync<T> requires a specific type T.
                try
                {
                    using var context = _connectionFactory.CreateContext(connectionString);
                    using var connection = context.Database.GetDbConnection();
                    using var command = connection.CreateCommand();
                    command.CommandText = query.QueryString;

                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync(cancellationToken);
                    }

                    using var reader = await command.ExecuteReaderAsync(cancellationToken);

                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        }
                        results.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    // Handle SQL execution errors
                    results.Add(new Dictionary<string, object> { { "Error", $"SQL Execution Error: {ex.Message}" } });
                }
            }
            else if (query.QueryType.Equals("MongoDB", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    results = await _mongoRepo.ExecuteQueryAsync(connectionString, query, cancellationToken);
                }
                 catch (Exception ex)
                {
                    // Handle MongoDB execution errors
                    results.Add(new Dictionary<string, object> { { "Error", $"MongoDB Execution Error: {ex.Message}" } });
                }
            }
            else
            {
                // Handle unknown query type
                 results.Add(new Dictionary<string, object> { { "Error", $"Unknown query type: {query.QueryType}" } });
            }

            // TODO: Apply date filtering if not done in the query

            return results;
        }

        public async Task ExportToExcelAsync(List<SavedQuery> savedQueries)
        {
            if (savedQueries == null || !savedQueries.Any())
            {
                // No data to export
                return;
            }

            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                foreach (var savedQuery in savedQueries)
                {
                    // Sanitize sheet name
                    string sheetName = savedQuery.Name.ReplaceInvalidChars();
                    if (sheetName.Length > 31) // Excel sheet name limit
                    {
                        sheetName = sheetName.Substring(0, 31);
                    }
                     if (string.IsNullOrWhiteSpace(sheetName))
                    {
                        sheetName = "Query"; // Default name if sanitized name is empty
                    }


                    var worksheet = workbook.Worksheets.Add(sheetName);

                    int currentRow = 1;

                    // Write query details
                    worksheet.Cell(currentRow, 1).Value = "Query Name:";
                    worksheet.Cell(currentRow, 2).Value = savedQuery.Name;
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Description:";
                    worksheet.Cell(currentRow, 2).Value = savedQuery.Description;
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = "Query Type:";
                    worksheet.Cell(currentRow, 2).Value = savedQuery.QueryType;
                    currentRow++;

                     if (savedQuery.QueryType == "MongoDB")
                    {
                        worksheet.Cell(currentRow, 1).Value = "Database:";
                        worksheet.Cell(currentRow, 2).Value = savedQuery.Database;
                        currentRow++;

                        worksheet.Cell(currentRow, 1).Value = "Collection:";
                        worksheet.Cell(currentRow, 2).Value = savedQuery.Collection;
                        currentRow++;
                    }


                    worksheet.Cell(currentRow, 1).Value = "Query String:";
                    worksheet.Cell(currentRow, 2).Value = savedQuery.QueryString;
                    currentRow++;

                    currentRow++; // Add a blank row before results

                    // Write results if available
                    if (savedQuery.LastRunResults != null && savedQuery.LastRunResults.Any())
                    {
                        var data = savedQuery.LastRunResults;
                        // Write headers
                        var headers = data.First().Keys.ToList();
                        for (int i = 0; i < headers.Count; i++)
                        {
                            worksheet.Cell(currentRow, i + 1).Value = headers[i];
                        }
                        currentRow++;

                        // Write data rows
                        for (int i = 0; i < data.Count; i++)
                        {
                            var rowData = data[i];
                            for (int j = 0; j < headers.Count; j++)
                            {
                                var header = headers[j];
                                worksheet.Cell(currentRow + i, j + 1).Value = rowData.ContainsKey(header) ? rowData[header]?.ToString() : "";
                            }
                        }

                        worksheet.Columns().AdjustToContents();
                    }
                    else
                    {
                        worksheet.Cell(currentRow, 1).Value = "No results available for this query.";
                    }
                }


                // Generate file path and save
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
                string fileName = $"DatabaseAsserts_AllResults_{timestamp}.xlsx";
                string directory = Path.GetTempPath();
                string filePath = Path.Combine(directory, fileName);

                workbook.SaveAs(filePath);

                // Open the file
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
            }
        }
    }
}