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

            // Hash file contents
            if (_fileSystem.FileExists(file))
            {
                using var stream = _fileSystem.OpenRead(file);
                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                }
            }
        }

        // Finalize calculation
        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }
}
