using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

public class PhysicalFileSystem : IFileSystem
{
    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public Task<string> ReadAllTextAsync(string path)
    {
        return File.ReadAllTextAsync(path);
    }

    public Task WriteAllTextAsync(string path, string contents)
    {
        return File.WriteAllTextAsync(path, contents);
    }

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public async Task<byte[]> ReadBytesAsync(string path, int count)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
        var buffer = new byte[count];
        int bytesRead = await stream.ReadAsync(buffer, 0, count);
        if (bytesRead < count)
        {
            Array.Resize(ref buffer, bytesRead);
        }
        return buffer;
    }

    public void CopyFile(string sourcePath, string destPath, bool overwrite = true)
    {
        try
        {
            File.Copy(sourcePath, destPath, overwrite);
        }
        catch (IOException)
        {
            // Fallback to stream-based copy with FileShare.ReadWrite to copy files open by Brave
            using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var destStream = new FileStream(destPath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            sourceStream.CopyTo(destStream);
        }
    }

    public void DeleteFile(string path)
    {
        File.Delete(path);
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        Directory.Delete(path, recursive);
    }

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
    {
        return Directory.EnumerateFiles(path, searchPattern, searchOption);
    }

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        return Directory.EnumerateDirectories(path);
    }

    public long GetFileLength(string path)
    {
        return new FileInfo(path).Length;
    }

    public Stream OpenRead(string path)
    {
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    }
}
