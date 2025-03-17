using Common.Repositories.Interno;
using SmartComponents.LocalEmbeddings;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Common.Services
{
    public class TextFileProcessor
    {
        private readonly RepositoryDbContext _dbContext;
        private readonly LocalEmbedder _embedder;

        public TextFileProcessor(LocalEmbedder embedder, RepositoryDbContext dbContext)
        {
            _embedder = embedder;
            _dbContext = dbContext;
        }

        public void ProcessFolder(string folderPath)
        {
            foreach (var filePath in MyDirectory.GetFiles(folderPath, "\\.cs|\\.js|\\.html|\\.xml", SearchOption.AllDirectories))
            {
                try
                {
                    string fileHash = ComputeFileHash(filePath);
                    if (_dbContext.FileChunks.Any(x => x.FileHash == fileHash))
                    {
                        Console.WriteLine($"Skipping already processed file: {filePath}");
                        continue;
                    }

                    string text = File.ReadAllText(filePath);
                    List<TextChunk> chunks = ChunkText(text);

                    foreach (var chunk in chunks)
                    {
                        var embedding = _embedder.Embed(chunk.Text);

                        var fileChunk = new FileChunkRecord
                        {
                            ChunkEnd = chunk.End,
                            ChunkStart = chunk.Start,
                            ChunkText = chunk.Text,
                            Embedding = embedding,
                            FileHash = fileHash,
                            FilePath = filePath
                        };

                        _dbContext.FileChunks.Add(fileChunk);
                    }

                    _dbContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"<Error processing {filePath}: {ex.Message}");
                }
            }
        }

        private List<TextChunk> ChunkText(string text)
        {
            List<TextChunk> chunks = new List<TextChunk>();

            // First try to chunk by C# patterns if it's a C# file
            //chunks = ChunkByCSharpPatterns(text);
            //chunks = ChunkBySlidingWindow(text);

            //// If pattern-based chunking didn't produce any chunks, fall back to improved delimiter-based chunking
            //if (chunks.Count == 0)
            //{
            chunks = ChunkByDelimiters(text);
            //}

            // If no chunks were created (e.g., for very small files), create a single chunk
            if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(text))
            {
                chunks.Add(new TextChunk
                {
                    Text = text,
                    Start = 0,
                    End = text.Length - 1
                });
            }

            return chunks;
        }

        /// <summary>
        /// Chunks C# code by recognizing common patterns like classes, methods, etc.
        /// </summary>
        private List<TextChunk> ChunkByCSharpPatterns(string text)
        {
            List<TextChunk> chunks = new List<TextChunk>();

            // Define patterns for C# constructs
            var patterns = new Dictionary<string, string>
            {
                { "namespace", @"namespace\s+[\w.]+\s*\{" },
                { "class", @"(public|private|protected|internal|static)?\s+class\s+\w+(\s*:\s*\w+)?\s*\{" },
                { "interface", @"(public|private|protected|internal|static)?\s+interface\s+\w+(\s*:\s*\w+)?\s*\{" },
                { "method", @"(public|private|protected|internal|static|virtual|override|abstract)?\s+[\w<>[\],\s]+\s+\w+\s*\([^)]*\)\s*(\s*where\s+[^{]+)?\s*\{" },
                { "property", @"(public|private|protected|internal|static|virtual|override|abstract)?\s+[\w<>[\],\s]+\s+\w+\s*\{\s*(get|set)" },
                { "enum", @"(public|private|protected|internal)?\s+enum\s+\w+\s*\{" }
            };

            // Find all matches for each pattern
            var allMatches = new List<(int start, int end, string text, string type)>();

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(text, pattern.Value);
                foreach (Match match in matches)
                {
                    // Find the closing brace for this construct
                    int openBracePos = text.IndexOf('{', match.Index);
                    if (openBracePos >= 0)
                    {
                        int closeBracePos = FindMatchingCloseBrace(text, openBracePos);
                        if (closeBracePos > openBracePos)
                        {
                            allMatches.Add((match.Index, closeBracePos + 1,
                                text.Substring(match.Index, closeBracePos - match.Index + 1),
                                pattern.Key));
                        }
                    }
                }
            }

            // Sort matches by start position
            allMatches = allMatches.OrderBy(m => m.start).ToList();

            // Filter out nested matches (keep only top-level constructs)
            var filteredMatches = new List<(int start, int end, string text, string type)>();
            foreach (var match in allMatches)
            {
                // Skip if this match is contained within another match
                bool isNested = filteredMatches.Any(m =>
                    match.start > m.start && match.end < m.end);

                if (!isNested)
                {
                    filteredMatches.Add(match);
                }
            }

            // Create chunks from the filtered matches
            foreach (var match in filteredMatches)
            {
                chunks.Add(new TextChunk
                {
                    Text = match.text,
                    Start = match.start,
                    End = match.end - 1
                });
            }

            return chunks;
        }

        /// <summary>
        /// Finds the position of the matching closing brace for an opening brace
        /// </summary>
        private int FindMatchingCloseBrace(string text, int openBracePos)
        {
            int braceCount = 1;
            for (int i = openBracePos + 1; i < text.Length; i++)
            {
                if (text[i] == '{') braceCount++;
                else if (text[i] == '}') braceCount--;

                if (braceCount == 0) return i;
            }
            return -1; // No matching brace found
        }

        /// <summary>
        /// Chunks text by delimiters like blank lines, with minimum chunk size
        /// </summary>
        private List<TextChunk> ChunkByDelimiters(string text, int minChunkSize = 100)
        {
            List<TextChunk> chunks = new List<TextChunk>();
            int currentPosition = 0;
            StringBuilder currentChunk = new StringBuilder();
            int chunkStartPosition = 0;

            while (currentPosition < text.Length)
            {
                int nextSeparator = text.IndexOf("\n\n", currentPosition, StringComparison.Ordinal);
                if (nextSeparator == -1)
                {
                    // Add remaining text
                    string remainingText = text.Substring(currentPosition);
                    currentChunk.Append(remainingText);

                    if (currentChunk.Length >= minChunkSize || chunks.Count == 0)
                    {
                        chunks.Add(new TextChunk
                        {
                            Text = currentChunk.ToString(),
                            Start = chunkStartPosition,
                            End = text.Length - 1
                        });
                    }
                    else if (chunks.Count > 0)
                    {
                        // Append to previous chunk if this one is too small
                        var lastChunk = chunks[chunks.Count - 1];
                        chunks[chunks.Count - 1] = new TextChunk
                        {
                            Text = lastChunk.Text + "\n\n" + currentChunk.ToString(),
                            Start = lastChunk.Start,
                            End = text.Length - 1
                        };
                    }
                    break;
                }

                string segment = text.Substring(currentPosition, nextSeparator - currentPosition);
                currentChunk.Append(segment);

                if (currentChunk.Length >= minChunkSize)
                {
                    chunks.Add(new TextChunk
                    {
                        Text = currentChunk.ToString(),
                        Start = chunkStartPosition,
                        End = nextSeparator - 1
                    });
                    currentChunk.Clear();
                    chunkStartPosition = nextSeparator + 2;
                }

                currentPosition = nextSeparator + 2; // Skip the double newline
            }

            return chunks;
        }

        /// <summary>
        /// Chunks text using a sliding window approach with overlap
        /// </summary>
        private List<TextChunk> ChunkBySlidingWindow(string text, int windowSize = 1000, int overlap = 200)
        {
            List<TextChunk> chunks = new List<TextChunk>();
            int textLength = text.Length;

            if (textLength <= windowSize)
            {
                chunks.Add(new TextChunk
                {
                    Text = text,
                    Start = 0,
                    End = textLength - 1
                });
                return chunks;
            }

            int position = 0;
            while (position < textLength)
            {
                int end = Math.Min(position + windowSize, textLength);

                // Try to find a better break point (newline, semicolon, etc.)
                if (end < textLength)
                {
                    int betterEnd = FindBetterBreakPoint(text, end);
                    if (betterEnd > 0) end = betterEnd;
                }

                chunks.Add(new TextChunk
                {
                    Text = text.Substring(position, end - position),
                    Start = position,
                    End = end - 1
                });

                position = end - overlap; // Create overlap
                if (position < 0) position = 0;
                if (position >= textLength) break;
            }

            return chunks;
        }

        /// <summary>
        /// Finds a better break point near the given position
        /// </summary>
        private int FindBetterBreakPoint(string text, int position)
        {
            // Look for a good break point within 100 characters
            int searchRange = 100;
            int endSearch = Math.Min(position + searchRange, text.Length);

            // Priority: line break, then semicolon, then period, then comma
            int lineBreak = text.IndexOf('\n', position, endSearch - position);
            if (lineBreak >= 0) return lineBreak + 1;

            int semicolon = text.IndexOf(';', position, endSearch - position);
            if (semicolon >= 0) return semicolon + 1;

            int period = text.IndexOf('.', position, endSearch - position);
            if (period >= 0) return period + 1;

            int comma = text.IndexOf(',', position, endSearch - position);
            if (comma >= 0) return comma + 1;

            return -1; // No better break point found
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    public record TextChunk
    {
        public string Text { get; init; }
        public int Start { get; init; }
        public int End { get; init; }
    }

    public class FileChunkRecord
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; }
        public int ChunkStart { get; set; }
        public int ChunkEnd { get; set; }
        public string ChunkText { get; set; }
        public EmbeddingF32? Embedding { get; set; }
        public string FileHash { get; set; }
    }
}