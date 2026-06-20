using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class BackupService : IBackupService
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupsRoot;
    private readonly ProfileHasher _profileHasher;
    private readonly BackupValidator _validator;
    private readonly BackupRestorer _restorer;

    public BackupService(IFileSystem fileSystem, ILogger<BackupService> logger, string? backupsRoot = null)
    {
        _fileSystem = fileSystem;
        _logger = logger;
        _backupsRoot = backupsRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor",
            "Backups"
        );

        _profileHasher = new ProfileHasher(_fileSystem);
        _validator = new BackupValidator(_fileSystem, _profileHasher, _backupsRoot);
        _restorer = new BackupRestorer(_fileSystem, _validator, _backupsRoot);
    }

    public async Task<BackupResult> CreateBackupAsync(BraveProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        _logger.LogInformation("Backup Started for profile: {ProfileName} at path {ProfilePath}", profile.DisplayName, profile.ProfilePath);

        if (!_fileSystem.DirectoryExists(profile.ProfilePath))
        {
            _logger.LogError("Backup Failed. Profile directory does not exist: {ProfilePath}", profile.ProfilePath);
            throw new ProfileFolderNotFoundException($"Profile folder not found at: {profile.ProfilePath}");
        }

        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var backupProfileDir = Path.Combine(backupDir, "profile");

        var stopwatch = Stopwatch.StartNew();
        var metadata = new BackupMetadata(
            BackupId: backupId,
            ProfileName: profile.DisplayName,
            ProfilePath: profile.ProfilePath,
            CreatedTimestamp: DateTime.UtcNow,
            BackupSize: 0,
            BackupVersion: "1.0",
            Status: BackupStatus.InProgress
        );

        try
        {
            _fileSystem.CreateDirectory(backupDir);
            _fileSystem.CreateDirectory(backupProfileDir);

            await SaveMetadataAsync(backupDir, metadata);

            var files = _fileSystem.EnumerateFiles(profile.ProfilePath, "*", SearchOption.AllDirectories)
                .Where(f => !ShouldExcludeFile(f))
                .ToList();
            int fileCount = 0;
            long totalBytes = 0;

            foreach (var file in files)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(profile.ProfilePath, file);
                    var destPath = Path.Combine(backupProfileDir, relativePath);

                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir) && !_fileSystem.DirectoryExists(destDir))
                    {
                        _fileSystem.CreateDirectory(destDir);
                    }

                    _fileSystem.CopyFile(file, destPath, true);
                    fileCount++;
                    totalBytes += _fileSystem.GetFileLength(file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy file {File} during backup. Skipping.", file);
                }
            }

            var backupHash = await _profileHasher.CalculateHashAsync(backupProfileDir);
            var profileHash = backupHash;

            stopwatch.Stop();
            var stats = new BackupStatistics(fileCount, totalBytes, stopwatch.Elapsed);

            var manifest = new BackupManifest(
                BackupId: backupId,
                CreatedAt: DateTime.UtcNow,
                ProfileName: profile.DisplayName,
                ProfilePath: profile.ProfilePath,
                Version: "1.0",
                ProfileHash: profileHash,
                FileCount: fileCount,
                BackupSize: totalBytes
            );

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(Path.Combine(backupDir, "manifest.json"), manifestJson);

            metadata = metadata with { Status = BackupStatus.Completed, BackupSize = totalBytes };
            await SaveMetadataAsync(backupDir, metadata);

            _logger.LogInformation("Backup Completed for profile: {ProfileName}. ID: {BackupId}, Size: {Size} bytes, Files: {Files}",
                profile.DisplayName, backupId, totalBytes, fileCount);

            return new BackupResult(true, metadata, null, stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup Failed for profile: {ProfileName}.", profile.DisplayName);

            try
            {
                if (_fileSystem.DirectoryExists(backupDir))
                {
                    metadata = metadata with { Status = BackupStatus.Failed };
                    await SaveMetadataAsync(backupDir, metadata);
                }
            }
            catch
            {

            }

            if (ex is BackupCorruptionException || ex is ProfileFolderNotFoundException)
            {
                throw;
            }

            throw new BackupException($"Backup creation failed for profile '{profile.DisplayName}': {ex.Message}", ex);
        }
    }

    public async Task<RestoreResult> RestoreBackupAsync(Guid backupId)
    {
        _logger.LogInformation("Restore Started for backup ID: {BackupId}", backupId);

        try
        {
            var result = await _restorer.RestoreBackupAsync(backupId);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Restore Completed for backup ID: {BackupId}. Restored {FilesCount} files ({BytesCount} bytes)",
                    backupId, result.FilesRestored, result.TotalBytesRestored);
            }
            else
            {
                _logger.LogError("Restore Failed for backup ID: {BackupId}. Reason: {Reason}", backupId, result.ErrorMessage);
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore Failed for backup ID: {BackupId} due to an unexpected exception.", backupId);
            throw new BackupRestoreException($"Failed to restore backup {backupId}: {ex.Message}", ex);
        }
    }

    public async Task<List<BackupMetadata>> GetBackupsAsync()
    {
        var list = new List<BackupMetadata>();
        if (!_fileSystem.DirectoryExists(_backupsRoot))
        {
            return list;
        }

        try
        {
            var dirs = _fileSystem.EnumerateDirectories(_backupsRoot);
            foreach (var dir in dirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!Guid.TryParse(dirName, out var backupId))
                {
                    continue;
                }

                var metadataPath = Path.Combine(dir, "metadata.json");
                if (_fileSystem.FileExists(metadataPath))
                {
                    try
                    {
                        var json = await _fileSystem.ReadAllTextAsync(metadataPath);
                        var metadata = JsonSerializer.Deserialize<BackupMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (metadata != null)
                        {
                            list.Add(metadata);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse metadata.json for backup {BackupId}", backupId);
                        list.Add(new BackupMetadata(backupId, "Unknown", "Unknown", DateTime.MinValue, 0, "1.0", BackupStatus.Corrupted));
                    }
                }
                else
                {

                    var manifestPath = Path.Combine(dir, "manifest.json");
                    if (_fileSystem.FileExists(manifestPath))
                    {
                        try
                        {
                            var json = await _fileSystem.ReadAllTextAsync(manifestPath);
                            var manifest = JsonSerializer.Deserialize<BackupManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (manifest != null)
                            {
                                list.Add(new BackupMetadata(manifest.BackupId, manifest.ProfileName, manifest.ProfilePath, manifest.CreatedAt, manifest.BackupSize, manifest.Version, BackupStatus.Completed));
                            }
                        }
                        catch
                        {
                            list.Add(new BackupMetadata(backupId, "Unknown", "Unknown", DateTime.MinValue, 0, "1.0", BackupStatus.Corrupted));
                        }
                    }
                    else
                    {
                        list.Add(new BackupMetadata(backupId, "Unknown", "Unknown", DateTime.MinValue, 0, "1.0", BackupStatus.Corrupted));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enumerating backups.");
        }

        return list;
    }

    public async Task<bool> DeleteBackupAsync(Guid backupId)
    {
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        if (!_fileSystem.DirectoryExists(backupDir))
        {
            return false;
        }

        try
        {
            _fileSystem.DeleteDirectory(backupDir, true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup {BackupId}", backupId);
            return false;
        }
    }

    public async Task<BackupValidationResult> ValidateBackupAsync(Guid backupId)
    {
        _logger.LogInformation("Validation Started for backup ID: {BackupId}", backupId);
        try
        {
            var result = await _validator.ValidateBackupAsync(backupId);
            if (!result.IsValid)
            {
                _logger.LogWarning("Validation Failed for backup ID: {BackupId}. Errors: {Errors}", backupId, string.Join("; ", result.Errors));
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation Failed with exception for backup ID: {BackupId}", backupId);
            return new BackupValidationResult(false, new List<string> { $"Unexpected validation error: {ex.Message}" }, new List<string>());
        }
    }

    private async Task SaveMetadataAsync(string backupDir, BackupMetadata metadata)
    {
        var metadataPath = Path.Combine(backupDir, "metadata.json");
        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await _fileSystem.WriteAllTextAsync(metadataPath, metadataJson);
    }

    private static bool ShouldExcludeFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) return true;

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
