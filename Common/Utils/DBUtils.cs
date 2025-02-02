namespace Common.Utils
{
    public static class DBUtils
    {
        public static string MainDBPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TugboatCaptainsPlayground",
            "Builds.db");
    }
}
