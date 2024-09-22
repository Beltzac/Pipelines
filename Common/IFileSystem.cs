namespace Common
{
    public interface IFileSystem
    {
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
    }
}
