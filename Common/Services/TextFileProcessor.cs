using Common.Repositories;
using SmartComponents.LocalEmbeddings;
using System.Security.Cryptography;

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
            foreach (var filePath in Directory.EnumerateFiles(folderPath, "*.cs"))
            {
                try
                {
                    string fileHash = ComputeFileHash(filePath);
                    if (_dbContext.FileChunks.FirstOrDefault(x => x.FileHash == fileHash) != null)
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
                        _dbContext.SaveChanges();
                    }
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
            int currentPosition = 0;

            while (currentPosition < text.Length)
            {
                int nextSeparator = text.IndexOf("\n\n", currentPosition, StringComparison.Ordinal);
                if (nextSeparator == -1)
                {
                    AddRemainingText(text, currentPosition, chunks);
                    break;
                }

                AddChunk(text, currentPosition, nextSeparator, chunks);
                currentPosition = nextSeparator + 2; // Skip the double newline
            }

            return chunks;
        }

        private void AddChunk(string text, int start, int end, List<TextChunk> chunks)
        {
            string chunkText = text.Substring(start, end - start);
            chunks.Add(new TextChunk
            {
                Text = chunkText,
                Start = start,
                End = end - 1 // Inclusive end position
            });
        }

        private void AddRemainingText(string text, int start, List<TextChunk> chunks)
        {
            string remainingText = text.Substring(start);
            if (remainingText.Length > 0)
            {
                chunks.Add(new TextChunk
                {
                    Text = remainingText,
                    Start = start,
                    End = text.Length - 1
                });
            }
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