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

    public async Task<string> ReadAllTextAsync(string path)
    {
        try
        {
            return await File.ReadAllTextAsync(path);
        }
        catch (IOException)
        {

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
    }

    public string ReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException)
        {

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
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
        if (!recursive)
        {
            Directory.Delete(path, false);
            return;
        }

        if (!Directory.Exists(path)) return;

        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
                    }
                }
                catch
                {

                }
            }

            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var attributes = File.GetAttributes(dir);
                    if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(dir, attributes & ~FileAttributes.ReadOnly);
                    }
                }
                catch
                {

                }
            }
        }
        catch
        {

        }

        int retries = 5;
        for (int i = 0; i < retries; i++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (Exception)
            {
                if (i == retries - 1) throw;
                System.Threading.Thread.Sleep(150);
            }
        }
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
