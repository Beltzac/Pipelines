using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Common.Services
{
    public class ThemeService : IThemeService
    {
        private readonly IConfigurationService _configService;
        private readonly IJSRuntime _jsRuntime;
        private bool _isDarkMode;

        public bool IsDarkMode => _isDarkMode;
        public event EventHandler ThemeChanged;

        public ThemeService(IConfigurationService configService, IJSRuntime jsRuntime)
        {
            _configService = configService;
            _jsRuntime = jsRuntime;
            var config = _configService.GetConfig();
            _isDarkMode = config.IsDarkMode;
            _ = UpdateThemeInBrowserAsync();
        }

        private async Task UpdateThemeInBrowserAsync()
        {
            await _jsRuntime.InvokeVoidAsync("themeManager.setTheme", _isDarkMode);
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
                await UpdateThemeInBrowserAsync();
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
