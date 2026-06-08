namespace Aevor.Application.Interfaces;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
    void CreateDirectory(string path);
}
