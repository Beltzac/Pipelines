using Common.Models;

namespace TugboatCaptainsPlayground.Services
{
    public class PaginationService<TKey, TValue>
    {
        private Func<Task<Dictionary<TKey, TValue>>> _getSourceItemsAsync;
        private Func<Task<Dictionary<TKey, TValue>>> _getTargetItemsAsync;
        private Func<TValue, TValue, Task<IDiffResult>> _getDiffAsync;

        private HashSet<TKey> _allKeys;
        private Dictionary<TKey, TValue> _sourceItems;
        private Dictionary<TKey, TValue> _targetItems;

        public async Task<List<IDiffResult>> GetPageAsync(int pageNumber, int pageSize)
        {
            var paginatedKeys = _allKeys
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var differences = new List<IDiffResult>();

            foreach (var key in paginatedKeys)
            {
                var sourceItem = _sourceItems.TryGetValue(key, out var source) ? source : default;
                var targetItem = _targetItems.TryGetValue(key, out var target) ? target : default;
                differences.Add(await _getDiffAsync(sourceItem, targetItem));
            }

            return differences;
        }

        public async Task InitializeAsync(
            Func<Task<Dictionary<TKey, TValue>>> getSourceItemsAsync,
            Func<Task<Dictionary<TKey, TValue>>> getTargetItemsAsync,
            Func<TValue, TValue, Task<IDiffResult>> getDiffAsync)
        {
            _getSourceItemsAsync = getSourceItemsAsync;
            _getTargetItemsAsync = getTargetItemsAsync;
            _getDiffAsync = getDiffAsync;

            _sourceItems = await _getSourceItemsAsync();
            _targetItems = await _getTargetItemsAsync();

            _allKeys = new HashSet<TKey>(_sourceItems.Keys.Union(_targetItems.Keys));
        }
    }
}
