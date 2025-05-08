using Microsoft.JSInterop;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Common.Services
{
    public sealed class ThemeService
    {
        readonly IJSRuntime _js;
        readonly IConfigurationService _config;
        bool _isJsInitialized;
        const string ThemeManagerJsPath = "./js/themeManager.js";

        public enum ThemeType { Default, Retro, Ninety }
        private static readonly Dictionary<ThemeType, string> ThemePaths = new()
        {
            [ThemeType.Default] = null,
            [ThemeType.Retro] = "/css/retrofuturist.css",
            [ThemeType.Ninety] = "/css/90s-theme.css"
        };

        public ThemeService(IJSRuntime js, IConfigurationService config)
        {
            _js = js;
            _config = config;
        }

        public async Task<ThemeType> GetActiveThemeAsync()
        {
            return Enum.Parse<ThemeType>(_config.GetConfig().Theme);
        }

        private async Task EnsureJsLoadedAsync()
        {
            if (_isJsInitialized) return;

            try
            {
                await _js.InvokeAsync<object>("import", ThemeManagerJsPath);
                _isJsInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading theme manager: {ex.Message}");
                throw;
            }
        }

        public async Task SetThemeAsync(ThemeType theme)
        {
            await EnsureJsLoadedAsync();
            await _js.InvokeVoidAsync("themeManager.unload");
            // Load new theme if needed
            if (theme != ThemeType.Default && ThemePaths.TryGetValue(theme, out var path))
            {
                await _js.InvokeVoidAsync("themeManager.load", path);
            }

            // Update config
            var config = _config.GetConfig();
            config.Theme = theme.ToString();
            await _config.SaveConfigAsync(config);
        }

        // call once on first render so the theme persists on refresh
        public async Task InitialiseAsync()
        {
            var theme = await GetActiveThemeAsync();
            await SetThemeAsync(theme);
        }
    }
}