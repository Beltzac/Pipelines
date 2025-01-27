using Common.Models;
using TugboatCaptainsPlayground.Services.Interfaces;

namespace TugboatCaptainsPlayground.Services
{
    public class PaginationService<TKey, TValue, TDiffResult> where TDiffResult : class, IDiffResult
    {
        private Func<Task<Dictionary<TKey, TValue>>> _getSourceItemsAsync;
        private Func<Task<Dictionary<TKey, TValue>>> _getTargetItemsAsync;
        private Func<TKey, TValue, TValue, Task<TDiffResult>> _getDiffAsync;
        private Func<TKey, TValue, TValue, TDiffResult, bool> _filter;

        private IComparesItems<TKey, TValue, TDiffResult> _state;

        private ITracksLoading? _loading => _state as ITracksLoading;

        public PaginationService(IComparesItems<TKey, TValue, TDiffResult> state,
            Func<Task<Dictionary<TKey, TValue>>> getSourceItemsAsync,
            Func<Task<Dictionary<TKey, TValue>>> getTargetItemsAsync,
            Func<TKey, TValue, TValue, Task<TDiffResult>> getDiffAsync,
            Func<TKey, TValue, TValue, TDiffResult, bool> filter)
        {
            _state = state;
            _getSourceItemsAsync = getSourceItemsAsync;
            _getTargetItemsAsync = getTargetItemsAsync;
            _getDiffAsync = getDiffAsync;
            _filter = filter;
        }

        public async Task GetPageAsync()
        {
            // In�cio do carregamento para obter a p�gina
            if (_loading != null)
            {
                _loading.IsLoading = true;
                _loading.ProgressValue = 0;
                _loading.ProgressLabel = "Obtendo p�gina...";
            }

            try
            {
                // Passo 1: Paginar as chaves
                if (_loading != null)
                {
                    _loading.ProgressValue = 25;
                    _loading.ProgressLabel = "Paginando as chaves...";
                }

                var paginatedKeys = _state.AllKeys
                    .Skip((_state.CurrentPage - 1) * _state.PageSize)
                    .Take(_state.PageSize)
                    .ToList();

                // Passo 2: Calcular diferen�as
                // Come�aremos o loop em 25% e iremos at� 100%.
                // A cada item, incrementamos a barra de progresso proporcionalmente.
                if (_loading != null)
                {
                    _loading.ProgressValue = 25;
                    _loading.ProgressLabel = "Calculando diferen�as...";
                }

                var differences = new List<TDiffResult>();
                int itemCount = paginatedKeys.Count;

                for (int i = 0; i < itemCount; i++)
                {
                    var key = paginatedKeys[i];
                    var sourceItem = _state.SourceValues.TryGetValue(key, out var source) ? source : default;
                    var targetItem = _state.TargetValues.TryGetValue(key, out var target) ? target : default;

                    differences.Add(await _getDiffAsync(key, sourceItem, targetItem));

                    // Atualizar o progresso depois de cada c�lculo de diff
                    if (_loading != null && itemCount > 0)
                    {
                        // Progresso vai de 25% at� 100%,
                        // ent�o calculamos qual fra��o do caminho percorrido dentro desse intervalo.
                        double fraction = (double)(i + 1) / itemCount; // vai de 0 at� 1
                        double rangeStart = 25;  // come�amos em 25%
                        double rangeEnd = 100;   // terminamos em 100%
                        double currentProgress = rangeStart + (rangeEnd - rangeStart) * fraction;

                        _loading.ProgressValue = (int)currentProgress;
                        _loading.ProgressLabel = $"Calculando diferen�as... {key} ({i + 1} / {itemCount})";
                    }
                }

                // Ao final do loop, salvamos o resultado
                _state.PageItems = differences;

                // Pode-se assegurar que o progresso seja 100%
                if (_loading != null)
                {
                    _loading.ProgressValue = 100;
                    _loading.ProgressLabel = "P�gina obtida com sucesso";
                }
            }
            finally
            {
                // Garantir que IsLoading seja desativado mesmo em caso de erro
                if (_loading != null)
                {
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
                _loading.ProgressValue = 0;
                _loading.ProgressLabel = "Iniciando...";
            }

            try
            {
                // Passo 1: Carregar valores de origem
                if (_loading != null)
                {
                    _loading.ProgressValue = 25;
                    _loading.ProgressLabel = "Carregando valores de origem...";
                }
                _state.SourceValues = await _getSourceItemsAsync();

                // Passo 2: Carregar valores de destino
                if (_loading != null)
                {
                    _loading.ProgressValue = 50;
                    _loading.ProgressLabel = "Carregando valores de destino...";
                }
                _state.TargetValues = await _getTargetItemsAsync();

                // Passo 3: Combinar chaves
                if (_loading != null)
                {
                    _loading.ProgressValue = 75;
                    _loading.ProgressLabel = "Processando chaves...";
                }
                _state.AllKeys = new HashSet<TKey>(_state.SourceValues.Keys.Union(_state.TargetValues.Keys));
                _state.TotalCount = _state.AllKeys.Count;

                // Conclus�o
                if (_loading != null)
                {
                    _loading.ProgressValue = 100;
                    _loading.ProgressLabel = "Conclu�do";
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
