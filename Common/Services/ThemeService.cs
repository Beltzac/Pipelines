using System;
using System.Threading.Tasks;

namespace Common.Services
{
    public class ThemeService : IThemeService
    {
        private readonly IConfigurationService _configService;
        private bool _isDarkMode;

        public bool IsDarkMode => _isDarkMode;
        public event EventHandler ThemeChanged;

        public ThemeService(IConfigurationService configService)
        {
            _configService = configService;
            var config = _configService.GetConfig();
            _isDarkMode = config.IsDarkMode;
        }

        public async Task ToggleThemeAsync()
        {
            await SetThemeAsync(!_isDarkMode);
        }

        public async Task SetThemeAsync(bool isDark)
        {
            if (_isDarkMode != isDark)
            {
                _isDarkMode = isDark;
                var config = _configService.GetConfig();
                config.IsDarkMode = isDark;
                await _configService.SaveConfigAsync(config);
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
