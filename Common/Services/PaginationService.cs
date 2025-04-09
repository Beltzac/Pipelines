using Common.Models;
using Common.Services.Interfaces;
using System.Collections.Concurrent;

namespace Common.Services
{
    public class PaginationService<TKey, TValue, TDiffResult> where TDiffResult : class, IDiffResult
    {
        private Func<Task<Dictionary<TKey, TValue>>> _getSourceItemsAsync;
        private Func<Task<Dictionary<TKey, TValue>>> _getTargetItemsAsync;
        private Func<TKey, TValue, TValue, TDiffResult> _getDiffAsync;
        private Func<TKey, TValue, TValue, TDiffResult, bool> _filter;

        private IComparesItems<TKey, TValue, TDiffResult> _state;

        private ITracksLoading? _loading => _state as ITracksLoading;

        public PaginationService(IComparesItems<TKey, TValue, TDiffResult> state,
            Func<Task<Dictionary<TKey, TValue>>> getSourceItemsAsync,
            Func<Task<Dictionary<TKey, TValue>>> getTargetItemsAsync,
            Func<TKey, TValue, TValue, TDiffResult> getDiffAsync,
            Func<TKey, TValue, TValue, TDiffResult, bool> filter)
        {
            _state = state;
            _getSourceItemsAsync = getSourceItemsAsync;
            _getTargetItemsAsync = getTargetItemsAsync;
            _getDiffAsync = getDiffAsync;
            _filter = filter;
        }

        private async Task<IList<TDiffResult>> GetAllFilteredDiffsAsync()
        {
            if (_loading != null)
            {
                _loading.IsLoading = true;
                _loading.ProgressValue = 0;
                _loading.ProgressLabel = "Calculando diffs...";
            }

            var keys = _state.AllKeys.ToList();
            var filteredDiffs = new ConcurrentBag<TDiffResult>();
            int totalKeys = keys.Count;
            int processedCount = 0;

            await Parallel.ForEachAsync(
                keys,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (key, cancellationToken) =>
                {
                    var sourceItem = _state.SourceValues.TryGetValue(key, out var s) ? s : default;
                    var targetItem = _state.TargetValues.TryGetValue(key, out var t) ? t : default;

                    // Calculate or retrieve cached diff using concurrent dictionary
                    var diffResult = _state.DiffCache.GetOrAdd(
                        key,
                        k => _getDiffAsync(k, sourceItem, targetItem));

                    if (_filter(key, sourceItem, targetItem, diffResult))
                    {
                        filteredDiffs.Add(diffResult);
                    }

                    // Update progress atomically
                    var currentCount = Interlocked.Increment(ref processedCount);
                    if (_loading != null)
                    {
                        double fraction = (double)currentCount / totalKeys;
                        _loading.ProgressValue = (int)(fraction * 100);
                        _loading.ProgressLabel = $"Processando diffs ({currentCount}/{totalKeys})";
                    }
                });

            if (_loading != null)
            {
                _loading.ProgressValue = 100;
                _loading.ProgressLabel = "Concluído";
                _loading.IsLoading = false;
            }

            return filteredDiffs.ToList();
        }

        public async Task GetPageAsync()
        {
            if (_loading != null)
            {
                _loading.IsLoading = true;
                _loading.ProgressValue = 0;
                _loading.ProgressLabel = "Obtendo página...";
            }

            try
            {
                var filteredDiffs = await GetAllFilteredDiffsAsync();
                _state.TotalCount = filteredDiffs.Count;

                // Create list of key-value pairs for sorting
                var keyedDiffs = filteredDiffs
                    .Select(d => new {
                        _state.DiffCache.First(kvp => kvp.Value == d).Key,
                        Diff = d
                    })
                    .OrderBy(x => x.Key)
                    .Select(x => x.Diff)
                    .ToList();

                // Pagination
                int skip = (_state.CurrentPage - 1) * _state.PageSize;
                var pagedDiffs = keyedDiffs
                    .Skip(skip)
                    .Take(_state.PageSize)
                    .ToList();

                _state.PageItems = pagedDiffs;
            }
            finally
            {
                if (_loading != null)
                {
                    _loading.ProgressValue = 100;
                    _loading.ProgressLabel = "Página obtida com sucesso";
                    _loading.IsLoading = false;
                }
            }
        }

        public async Task InitializeAsync()
        {
            // Iniciando carregamento
            if (_loading != null)
            {
                _loading.IsLoading = true;
                _loading.ProgressValue = 25;
                _loading.ProgressLabel = "Iniciando...";
            }

            try
            {
                var getSourceTask = _getSourceItemsAsync();
                var getTargetTask = _getTargetItemsAsync();

                var tasksWithProgress = new[]
                {
                    new { Task = getSourceTask, Label = "Carregado valores fonte..." },
                    new { Task = getTargetTask, Label = "Carregado valores alvo..." }
                };

                // Update progress using task labels
                int completed = 0;
                await foreach (var completedTask in Task.WhenEach(tasksWithProgress.Select(t => t.Task)))
                {
                    var taskInfo = tasksWithProgress.First(t => t.Task == completedTask);
                    completed++;
                    if (_loading != null)
                    {
                        var progress = (double)completed / tasksWithProgress.Length;
                        _loading.ProgressValue = (int)(progress * 100);
                        _loading.ProgressLabel = $"{taskInfo.Label} ({completed}/{tasksWithProgress.Length})";
                    }
                }

                // Obter resultados
                _state.SourceValues = await getSourceTask;
                _state.TargetValues = await getTargetTask;

                _state.AllKeys = new HashSet<TKey>(_state.SourceValues.Keys.Union(_state.TargetValues.Keys));
                _state.TotalCount = _state.AllKeys.Count;

                // Limpar cache
                _state.DiffCache.Clear();

                // Conclusão
                if (_loading != null)
                {
                    _loading.ProgressValue = 100;
                    _loading.ProgressLabel = "Concluído";
                }
            }
            finally
            {
                // Garantir que IsLoading seja desativado em caso de erros
                if (_loading != null)
                {
                    _loading.IsLoading = false;
                }
            }
        }
    }
}
