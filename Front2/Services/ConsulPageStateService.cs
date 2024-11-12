using Common.Models;

namespace Front2.Services
{
    public class ConsulPageStateService
    {
        private string _selectedConsulEnv;
        private Dictionary<string, ConsulKeyValue> _consulKeyValues = new Dictionary<string, ConsulKeyValue>();
        private List<string> _visibleKeys = new List<string>();
        private string _searchTerm = string.Empty;
        private bool _isRecursive = false;
        private bool _showInvalidOnly = false;

        public string SelectedConsulEnv
        {
            get => _selectedConsulEnv;
            set
            {
                _selectedConsulEnv = value;
                OnChange?.Invoke();
            }
        }

        public IReadOnlyDictionary<string, ConsulKeyValue> ConsulKeyValues => _consulKeyValues;

        public void SetConsulKeyValues(Dictionary<string, ConsulKeyValue> keyValues)
        {
            _consulKeyValues = keyValues ?? new Dictionary<string, ConsulKeyValue>();
            OnChange?.Invoke();
        }

        public IReadOnlyList<string> VisibleKeys => _visibleKeys;

        public void AddVisibleKey(string key)
        {
            _visibleKeys.Add(key);
            OnChange?.Invoke();
        }

        public void RemoveVisibleKey(string key)
        {
            if (_visibleKeys.Remove(key))
            {
                OnChange?.Invoke();
            }
        }

        public void ClearVisibleKeys()
        {
            _visibleKeys.Clear();
            OnChange?.Invoke();
        }

        public void SetVisibleKeys(IEnumerable<string> keys)
        {
            _visibleKeys.Clear();
            _visibleKeys.AddRange(keys);
            OnChange?.Invoke();
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                _searchTerm = value;
                OnChange?.Invoke();
            }
        }

        public bool IsRecursive
        {
            get => _isRecursive;
            set
            {
                _isRecursive = value;
                OnChange?.Invoke();
            }
        }

        public bool ShowInvalidOnly
        {
            get => _showInvalidOnly;
            set
            {
                _showInvalidOnly = value;
                OnChange?.Invoke();
            }
        }

        public event Action OnChange;
    }
}
