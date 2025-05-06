using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace Common.Services
{
    public sealed class ThemeService
    {
        readonly IJSRuntime _js;
        const string ActiveThemeKey = "active-theme";

        public enum ThemeType { Default, Retro, Ninety }
        private static readonly Dictionary<ThemeType, string> ThemePaths = new()
        {
            [ThemeType.Default] = null,
            [ThemeType.Retro] = "/css/retrofuturist.css",
            [ThemeType.Ninety] = "/css/90s-theme.css"
        };

        public ThemeService(IJSRuntime js) => _js = js;

        public async Task<ThemeType> GetActiveThemeAsync()
        {
            var themeName = await _js.InvokeAsync<string>("localStorage.getItem", ActiveThemeKey);
            if (Enum.TryParse<ThemeType>(themeName, out var theme))
            {
                return theme;
            }
            return ThemeType.Default;
        }

        public async Task SetThemeAsync(ThemeType theme)
        {
            // First unload any existing theme
            await _js.InvokeVoidAsync("themeManager.unload");

            // Load new theme if needed
            if (theme != ThemeType.Default && ThemePaths.TryGetValue(theme, out var path))
            {
                await _js.InvokeVoidAsync("themeManager.load", path);
            }

            await _js.InvokeVoidAsync("localStorage.setItem", ActiveThemeKey, theme.ToString());
        }

        // call once on first render so the theme persists on refresh
        public async Task InitialiseAsync()
        {
            var theme = await GetActiveThemeAsync();
            await SetThemeAsync(theme);
        }
    }
}