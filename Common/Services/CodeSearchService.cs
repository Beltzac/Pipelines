using Common.Repositories.Interno;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartComponents.LocalEmbeddings;

namespace Common.Services
{
    public class CodeSearchService
    {
        private readonly RepositoryDbContext _dbContext;
        private readonly LocalEmbedder _embedder;
        private readonly ILogger<CodeSearchService> _logger;

        public CodeSearchService(
            RepositoryDbContext dbContext,
            LocalEmbedder embedder,
            ILogger<CodeSearchService> logger)
        {
            _dbContext = dbContext;
            _embedder = embedder;
            _logger = logger;
        }

        public async Task<List<SearchResult>> SearchCodeAsync(string query, float minSimilarity = 0.6f, int maxResults = 20)
        {
            _logger.LogInformation($"Searching code with query: {query}");

            // Create embedding for the search query
            var queryEmbedding = _embedder.Embed(query);

            // Get all file chunks with embeddings
            var fileChunks = await _dbContext.FileChunks
                .Where(c => c.Embedding != null)
                .ToListAsync();

            if (!fileChunks.Any())
            {
                _logger.LogWarning("No indexed code files found. Please index repositories first.");
                return new List<SearchResult>();
            }

            _logger.LogInformation($"Found {fileChunks.Count} indexed chunks to search through");

            // Create pairs of (chunk, embedding) for the search
            var embeddingPairs = fileChunks
                .Where(chunk => chunk.Embedding != null)
                .Select(chunk => (Item: chunk, Embedding: chunk.Embedding.Value))
                .ToList();

            // Use LocalEmbedder.FindClosest to find the most similar chunks
            var results = LocalEmbedder.FindClosestWithScore(queryEmbedding, embeddingPairs, maxResults, minSimilarity);

            // Convert to SearchResult objects
            var searchResults = new List<SearchResult>();

            foreach (var result in results)
            {
                searchResults.Add(new SearchResult
                {
                    FilePath = result.Item.FilePath,
                    ChunkText = result.Item.ChunkText,
                    Similarity = result.Similarity,
                    StartLine = result.Item.ChunkStart,
                    EndLine = result.Item.ChunkEnd
                });
            }

            return searchResults;
        }
    }

    public class SearchResult
    {
        public string FilePath { get; set; }
        public string ChunkText { get; set; }
        public float Similarity { get; set; }
        public int StartLine { get; set; }
        public int EndLine { get; set; }

        // Helper property to get just the filename without the full path
        public string FileName => Path.GetFileName(FilePath);

        // Helper property to get a shortened version of the file path
        public string ShortenedPath
        {
            get
            {
                var parts = FilePath.Split(Path.DirectorySeparatorChar);
                if (parts.Length <= 3)
                    return FilePath;

                return $"...{Path.DirectorySeparatorChar}{string.Join(Path.DirectorySeparatorChar, parts.Skip(parts.Length - 3))}";
            }
        }
    }
}