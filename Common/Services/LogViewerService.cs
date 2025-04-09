using Common.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Common.Services
{
    public class LogViewerService
    {
        // No longer needed, using LogUtils
        // Regex to capture Timestamp, Level, and Message based on default Serilog file format
        // Example: 2025-04-09 11:00:00.123 +00:00 [INF] This is a log message.
        // Adjust regex if the output template in Program.cs is changed.
        private static readonly Regex LogEntryRegex = new Regex(
            @"^(?<Timestamp>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{3}\s[+-]\d{2}:\d{2})\s\[(?<Level>\w+)\]\s(?<Message>.*)",
            RegexOptions.Compiled);

        public async Task<List<LogEntry>> GetLogEntriesAsync(string? levelFilter = null, string? searchTerm = null)
        {
            var logEntries = new List<LogEntry>();
            var logDirPath = Common.Utils.LogUtils.LogDirectoryPath; // Use the utility property

            if (!Directory.Exists(logDirPath))
            {
                return logEntries; // Return empty list if directory doesn't exist
            }

            // Find the most recent log file (assuming daily rolling like log-.txt)
            var latestLogFile = Directory.GetFiles(logDirPath, "log-*.txt")
                                         .OrderByDescending(f => f)
                                         .FirstOrDefault();

            if (string.IsNullOrEmpty(latestLogFile))
            {
                return logEntries; // Return empty list if no log file found
            }

            try
            {
                // Read file asynchronously to avoid blocking
                var lines = await File.ReadAllLinesAsync(latestLogFile);
                LogEntry? currentEntry = null;

                foreach (var line in lines)
                {
                    var match = LogEntryRegex.Match(line);
                    if (match.Success)
                    {
                        // If we were building a multi-line entry, add it before starting the new one
                        if (currentEntry != null)
                        {
                            logEntries.Add(currentEntry);
                        }

                        currentEntry = new LogEntry
                        {
                            Timestamp = DateTime.ParseExact(match.Groups["Timestamp"].Value, "yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                            Level = match.Groups["Level"].Value,
                            Message = match.Groups["Message"].Value.Trim()
                        };
                    }
                    else if (currentEntry != null && !string.IsNullOrWhiteSpace(line))
                    {
                        // Append multi-line messages or exception details
                        // Simple approach: append to message. Could be refined to specifically capture exceptions.
                        currentEntry.Message += Environment.NewLine + line.Trim();
                        // Consider adding specific Exception property handling here if needed
                    }
                }
                // Add the last entry if it exists
                if (currentEntry != null)
                {
                    logEntries.Add(currentEntry);
                }
            }
            catch (IOException ex)
            {
                // Handle potential file access issues (e.g., file locked)
                Console.WriteLine($"Error reading log file '{latestLogFile}': {ex.Message}");
                // Optionally return a specific error entry or re-throw
                logEntries.Add(new LogEntry { Timestamp = DateTime.UtcNow, Level = "ERR", Message = $"Error reading log file: {ex.Message}" });
            }
            catch (Exception ex) // Catch other potential errors during parsing
            {
                Console.WriteLine($"Error processing log file '{latestLogFile}': {ex.Message}");
                logEntries.Add(new LogEntry { Timestamp = DateTime.UtcNow, Level = "ERR", Message = $"Error processing log file: {ex.Message}" });
            }

            // Apply filters
            IEnumerable<LogEntry> filteredEntries = logEntries;

            if (!string.IsNullOrEmpty(levelFilter) && levelFilter != "All")
            {
                filteredEntries = filteredEntries.Where(e => e.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                filteredEntries = filteredEntries.Where(e =>
                    (e.Message?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Exception?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)); // Include exception in search if populated
            }

            // Return filtered list, ordered by timestamp descending
            return filteredEntries.OrderByDescending(e => e.Timestamp).ToList();
        }
    }
}