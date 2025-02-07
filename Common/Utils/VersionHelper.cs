namespace Common.Utils
{
    public static class VersionHelper
    {

        /// <summary>
        /// Obtém a versão atual da aplicação.
        /// </summary>
        public static Version GetCurrentVersion()
        {
#if DEBUG
            // Return a default version when in debug mode.
            return Version.Parse("0.0.0");
#else
            // Return the version from your Git tag (or similar) in release mode.
            try
            {
                return Version.Parse(ThisAssembly.Git.Tag);
            }
            catch
            {
            }

            try
            {
                return Version.Parse(ThisAssembly.Git.BaseTag);
            }
            catch
            {
            }

            try
            {
                return Version.Parse($"{ThisAssembly.Git.SemVer.Major}.{ThisAssembly.Git.SemVer.Minor}.{ThisAssembly.Git.SemVer.Patch}");
            }
            catch
            {
            }

            return Version.Parse("0.0.0");
#endif
        }
    }
}