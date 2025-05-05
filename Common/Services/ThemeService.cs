using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace Common.Services
{
    public sealed class ThemeService
    {
        readonly IJSRuntime _js;
        const string ThemeEnabledKey = "theme-enabled";

        public ThemeService(IJSRuntime js) => _js = js;

        public async Task<bool> IsThemeEnabledAsync()
        {
            var enabled = await _js.InvokeAsync<string>("localStorage.getItem", ThemeEnabledKey);
            return enabled == "true";
        }

        public async Task EnableThemeAsync()
        {
            await _js.InvokeVoidAsync("themeManager.load", "/css/90s-theme.css");
            await _js.InvokeVoidAsync("localStorage.setItem", ThemeEnabledKey, "true");
        }

        public async Task DisableThemeAsync()
        {
            await _js.InvokeVoidAsync("themeManager.unload");
            await _js.InvokeVoidAsync("localStorage.setItem", ThemeEnabledKey, "false");
        }

        // call once on first render so the theme persists on refresh
        public async Task InitialiseAsync()
        {
            if (await IsThemeEnabledAsync())
            {
                await EnableThemeAsync();
            }
            else
            {
                await DisableThemeAsync(); // Ensure theme is unloaded if not enabled
            }
        }
    }
}