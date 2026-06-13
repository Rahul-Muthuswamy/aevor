using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

public class ProfileHasher
{
    private readonly IFileSystem _fileSystem;

    public ProfileHasher(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<string> CalculateHashAsync(string directoryPath)
    {
        if (!_fileSystem.DirectoryExists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        // Order files to ensure deterministic hashing across runs
        var files = _fileSystem.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
            .Where(f => !ShouldExcludeFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var sha256 = SHA256.Create();

        if (files.Count == 0)
        {
            var emptyBytes = sha256.ComputeHash(Array.Empty<byte>());
            return Convert.ToHexString(emptyBytes).ToLowerInvariant();
        }

        for (int i = 0; i < files.Count; i++)
        {
            var file = files[i];
            var relativePath = Path.GetRelativePath(directoryPath, file).Replace('\\', '/');
            var relativePathBytes = Encoding.UTF8.GetBytes(relativePath);

            // Hash the relative path to distinguish directories and file locations
            sha256.TransformBlock(relativePathBytes, 0, relativePathBytes.Length, relativePathBytes, 0);

            // Hash file contents/metadata
            if (_fileSystem.FileExists(file))
            {
                var fileName = Path.GetFileName(file);
                bool shouldHashContent = fileName.Equals("Preferences", StringComparison.OrdinalIgnoreCase) ||
                                         fileName.Equals("Secure Preferences", StringComparison.OrdinalIgnoreCase) ||
                                         fileName.Equals("Bookmarks", StringComparison.OrdinalIgnoreCase) ||
                                         fileName.Equals("Bookmarks.bak", StringComparison.OrdinalIgnoreCase);

                if (shouldHashContent)
                {
                    using var stream = _fileSystem.OpenRead(file);
                    var buffer = new byte[65536];
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                    }
                }
                else
                {
                    var fileLength = _fileSystem.GetFileLength(file);
                    var lengthBytes = BitConverter.GetBytes(fileLength);
                    sha256.TransformBlock(lengthBytes, 0, lengthBytes.Length, lengthBytes, 0);
                }
            }
        }

        // Finalize calculation
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    private static bool ShouldExcludeFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) return true;

        // Exclude lock and socket files
        if (fileName.Equals("lockfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("parent.lock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonLock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonCookie", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonSocket", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("socket", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Exclude cache and service worker directories
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => p.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals("Code Cache", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals("GPUCache", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals("Service Worker", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals("CacheStorage", StringComparison.OrdinalIgnoreCase) ||
                           p.Equals("DawnCache", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
