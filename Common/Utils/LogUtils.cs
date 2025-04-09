using System;
using System.IO;

namespace Common.Utils
{
    public static class LogUtils
    {
        public static string LogDirectoryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TugboatCaptainsPlayground",
            "Logs"); // Consistent directory name
    }
}