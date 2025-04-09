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

        public async Task<List<LogEntry>> GetLogEntriesAsync(string? levelFilter = null, string? searchTerm = null, int minutesFilter = 10) // Added minutesFilter
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

            var cutoffTime = DateTime.UtcNow.AddMinutes(-minutesFilter);
            var rawLines = new List<string>(); // Store lines read from file
            int maxRetries = 3;
            int delayMilliseconds = 200;

            // --- File Reading with Retry ---
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    rawLines.Clear(); // Clear lines for retry
                    using (var fileStream = new FileStream(latestLogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var streamReader = new StreamReader(fileStream))
                    {
                        string? line;
                        while ((line = await streamReader.ReadLineAsync()) != null)
                        {
                            rawLines.Add(line);
                        }
                    }
                    break; // Success, exit retry loop
                }
                catch (IOException ex)
                {
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"Error reading log file '{latestLogFile}' after {maxRetries} attempts: {ex.Message}");
                        logEntries.Add(new LogEntry { Timestamp = DateTime.UtcNow, Level = "ERR", Message = $"Error reading log file: {ex.Message}" });
                        return logEntries; // Return early with error
                    }
                    await Task.Delay(delayMilliseconds);
                }
                catch (Exception ex) // Catch other potential errors during reading
                {
                     Console.WriteLine($"Error processing log file '{latestLogFile}': {ex.Message}");
                     logEntries.Add(new LogEntry { Timestamp = DateTime.UtcNow, Level = "ERR", Message = $"Error processing log file: {ex.Message}" });
                     return logEntries; // Return early with error
                }
            }

            // --- Parsing Logic (Optimized with Time Filter) ---
            var tempEntries = new List<LogEntry>(); // Store parsed entries temporarily
            LogEntry? currentEntry = null;

            // Iterate backwards through the raw lines
            for (int i = rawLines.Count - 1; i >= 0; i--)
            {
                var line = rawLines[i];
                var match = LogEntryRegex.Match(line);

                if (match.Success)
                {
                    // Finish the previous multi-line entry (if any) before starting new one
                    if (currentEntry != null)
                    {
                        tempEntries.Add(currentEntry);
                    }

                    var timestamp = DateTime.ParseExact(match.Groups["Timestamp"].Value, "yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

                    // Stop reading if we've gone past the cutoff time
                    if (timestamp < cutoffTime)
                    {
                        currentEntry = null; // Discard any partial entry being built
                        break; // Exit loop, we have enough data
                    }

                    currentEntry = new LogEntry
                    {
                        Timestamp = timestamp,
                        Level = match.Groups["Level"].Value,
                        Message = match.Groups["Message"].Value.Trim()
                    };
                }
                else if (currentEntry != null && !string.IsNullOrWhiteSpace(line))
                {
                    // Prepend multi-line messages when reading backwards
                    currentEntry.Message = line.Trim() + Environment.NewLine + currentEntry.Message;
                }
                else
                {
                    // If we encounter a non-matching line and aren't building an entry,
                    // check if it *might* be the start of a timestamp older than cutoff.
                    // This is a heuristic to potentially stop earlier on malformed/old files.
                    if (line.Length > 20 && DateTime.TryParse(line.Substring(0, 19), out var potentialTimestamp))
                    {
                         if (potentialTimestamp.ToUniversalTime() < cutoffTime) break;
                    }
                     currentEntry = null; // Reset if line doesn't match and isn't part of multi-line
                }
            }
            // Add the very last entry being built (if any)
            if (currentEntry != null)
            {
                 tempEntries.Add(currentEntry);
            }

            // Reverse the list to get chronological order and assign to logEntries
            logEntries = tempEntries;
            logEntries.Reverse();

            // Apply filters
            // Apply level and search filters AFTER time filtering and parsing
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

            // Return the filtered list (already in chronological order)
            return filteredEntries.ToList();
        }
    }
}