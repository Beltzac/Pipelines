using System;
using System.Threading.Tasks;

namespace Common.Services
{
    public interface IThemeService
    {
        bool IsDarkMode { get; }
        event EventHandler ThemeChanged;
        Task ToggleThemeAsync();
        Task SetThemeAsync(bool isDark);
    }
}
