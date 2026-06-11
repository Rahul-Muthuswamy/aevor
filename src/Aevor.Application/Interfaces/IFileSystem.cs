using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Aevor.Application.Interfaces;

public interface IFileSystem
{
    bool DirectoryExists(string path);
    bool FileExists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string contents);
    void CreateDirectory(string path);
    Task<byte[]> ReadBytesAsync(string path, int count);

    void CopyFile(string sourcePath, string destPath, bool overwrite = true);
    void DeleteFile(string path);
    void DeleteDirectory(string path, bool recursive);
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateDirectories(string path);
    long GetFileLength(string path);
    Stream OpenRead(string path);
}
