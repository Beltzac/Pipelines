using System.Text;
using Vanara.PInvoke;

namespace Common.Utils
{
    public static class WindowUtils
    {
        public static List<string> EnumerarJanelas()
        {
            var windowList = new List<string>();

            User32.EnumWindows((hWnd, lParam) =>
            {
                if (User32.IsWindowVisible(hWnd))
                {
                    int length = User32.GetWindowTextLength(hWnd);
                    StringBuilder title = new StringBuilder(length + 1);
                    User32.GetWindowText(hWnd, title, title.Capacity);

                    if (!string.IsNullOrWhiteSpace(title.ToString()))
                    {
                        windowList.Add(title.ToString());
                        Console.WriteLine("Window: " + title.ToString());
                    }
                }
                return true;
            }, nint.Zero);
            return windowList;
        }
    }
}
