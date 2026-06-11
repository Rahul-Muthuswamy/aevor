using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class BackupRestorer
{
    private readonly IFileSystem _fileSystem;
    private readonly BackupValidator _validator;
    private readonly string _backupsRoot;

    public BackupRestorer(IFileSystem fileSystem, BackupValidator validator, string backupsRoot)
    {
        _fileSystem = fileSystem;
        _validator = validator;
        _backupsRoot = backupsRoot;
    }

    public async Task<RestoreResult> RestoreBackupAsync(Guid backupId)
    {
        // 1. Verify Backup
        var validationResult = await _validator.ValidateBackupAsync(backupId);
        if (!validationResult.IsValid)
        {
            var errorsStr = string.Join("; ", validationResult.Errors);
            return new RestoreResult(false, $"Backup validation failed: {errorsStr}", 0, 0);
        }

        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var manifestPath = Path.Combine(backupDir, "manifest.json");
        
        BackupManifest manifest;
        try
        {
            var manifestJson = await _fileSystem.ReadAllTextAsync(manifestPath);
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (Exception ex)
        {
            return new RestoreResult(false, $"Failed to read or parse manifest for restore: {ex.Message}", 0, 0);
        }

        var targetProfilePath = manifest.ProfilePath;
        var backupProfilePath = Path.Combine(backupDir, "profile");

        if (string.IsNullOrWhiteSpace(targetProfilePath))
        {
            return new RestoreResult(false, "Target profile path in manifest is invalid.", 0, 0);
        }

        try
        {
            // 2. Clear target profile if it exists to ensure a clean restoration
            if (_fileSystem.DirectoryExists(targetProfilePath))
            {
                try
                {
                    _fileSystem.DeleteDirectory(targetProfilePath, true);
                }
                catch (Exception ex)
                {
                    return new RestoreResult(false, $"Failed to clear target profile directory '{targetProfilePath}': {ex.Message}", 0, 0);
                }
            }

            _fileSystem.CreateDirectory(targetProfilePath);

            // 3. Restore files
            var files = _fileSystem.EnumerateFiles(backupProfilePath, "*", SearchOption.AllDirectories).ToList();
            int filesRestoredCount = 0;
            long totalBytesRestored = 0;

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(backupProfilePath, file);
                var destPath = Path.Combine(targetProfilePath, relativePath);

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !_fileSystem.DirectoryExists(destDir))
                {
                    _fileSystem.CreateDirectory(destDir);
                }

                _fileSystem.CopyFile(file, destPath, true);
                filesRestoredCount++;
                totalBytesRestored += _fileSystem.GetFileLength(file);
            }

            return new RestoreResult(true, null, filesRestoredCount, totalBytesRestored);
        }
        catch (Exception ex)
        {
            return new RestoreResult(false, $"Restore failed due to an exception: {ex.Message}", 0, 0);
        }
    }
}
