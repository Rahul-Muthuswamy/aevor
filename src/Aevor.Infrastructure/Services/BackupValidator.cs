using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aevor.Application.Interfaces;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class BackupValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly ProfileHasher _profileHasher;
    private readonly string _backupsRoot;

    public BackupValidator(IFileSystem fileSystem, ProfileHasher profileHasher, string backupsRoot)
    {
        _fileSystem = fileSystem;
        _profileHasher = profileHasher;
        _backupsRoot = backupsRoot;
    }

    public async Task<BackupValidationResult> ValidateBackupAsync(Guid backupId)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        if (!_fileSystem.DirectoryExists(backupDir))
        {
            errors.Add($"Backup directory does not exist for backup ID: {backupId}");
            return new BackupValidationResult(false, errors, warnings);
        }

        var manifestPath = Path.Combine(backupDir, "manifest.json");
        if (!_fileSystem.FileExists(manifestPath))
        {
            errors.Add("manifest.json is missing.");
            return new BackupValidationResult(false, errors, warnings);
        }

        var metadataPath = Path.Combine(backupDir, "metadata.json");
        if (!_fileSystem.FileExists(metadataPath))
        {
            warnings.Add("metadata.json is missing.");
        }

        var profileDir = Path.Combine(backupDir, "profile");
        if (!_fileSystem.DirectoryExists(profileDir))
        {
            errors.Add("Profile backup subfolder is missing.");
            return new BackupValidationResult(false, errors, warnings);
        }

        BackupManifest? manifest = null;
        try
        {
            var manifestJson = await _fileSystem.ReadAllTextAsync(manifestPath);
            manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to read or parse manifest.json: {ex.Message}");
        }

        if (manifest == null)
        {
            errors.Add("manifest.json is corrupted.");
            return new BackupValidationResult(false, errors, warnings);
        }

        if (manifest.BackupId != backupId)
        {
            errors.Add($"Backup ID mismatch in manifest. Expected: {backupId}, Found: {manifest.BackupId}");
        }

        int actualFileCount = 0;
        try
        {
            actualFileCount = _fileSystem.EnumerateFiles(profileDir, "*", SearchOption.AllDirectories).Count();
            if (actualFileCount != manifest.FileCount)
            {
                errors.Add($"File count mismatch. Manifest: {manifest.FileCount}, Actual: {actualFileCount}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to enumerate backup files: {ex.Message}");
        }

        if (errors.Count == 0)
        {
            try
            {
                var calculatedHash = await _profileHasher.CalculateHashAsync(profileDir);
                if (!calculatedHash.Equals(manifest.ProfileHash, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Profile hash mismatch. Manifest: {manifest.ProfileHash}, Calculated: {calculatedHash}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to calculate backup hash: {ex.Message}");
            }
        }

        return new BackupValidationResult(errors.Count == 0, errors, warnings);
    }
}
