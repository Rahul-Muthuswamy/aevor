using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class BackupServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly NullLogger<BackupService> _logger = NullLogger<BackupService>.Instance;
    private readonly string _backupsRoot = "C:\\Backups";
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _service = new BackupService(_fileSystem, _logger, _backupsRoot);
    }

    [Fact]
    public async Task CreateBackupAsync_ValidProfile_ShouldCreateBackupSuccessfully()
    {
        // Arrange
        var profile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");
        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(true);
        _fileSystem.EnumerateFiles("C:\\Brave\\Default", "*", SearchOption.AllDirectories)
            .Returns(new[] { "C:\\Brave\\Default\\Preferences" });
        _fileSystem.FileExists("C:\\Brave\\Default\\Preferences").Returns(true);
        _fileSystem.GetFileLength("C:\\Brave\\Default\\Preferences").Returns(12L);
        _fileSystem.OpenRead(Arg.Any<string>()).Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("test content")));

        // To make the hashing validation succeed, the copied profile must also be found:
        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("profile"))).Returns(true);
        _fileSystem.EnumerateFiles(Arg.Is<string>(s => s.Contains("profile")), "*", SearchOption.AllDirectories)
            .Returns(x => new[] { Path.Combine(x.ArgAt<string>(0), "Preferences") });
        _fileSystem.FileExists(Arg.Is<string>(s => s.Contains("profile") && s.Contains("Preferences"))).Returns(true);

        // Act
        var result = await _service.CreateBackupAsync(profile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata.Should().NotBeNull();
        result.Metadata!.Status.Should().Be(BackupStatus.Completed);
        result.Metadata.ProfileName.Should().Be("Default");
        result.Metadata.BackupSize.Should().Be(12L);
        result.Statistics.Should().NotBeNull();
        result.Statistics!.FileCount.Should().Be(1);

        // Verify copy and write operations
        _fileSystem.Received(1).CopyFile("C:\\Brave\\Default\\Preferences", Arg.Any<string>(), true);
        await _fileSystem.Received(1).WriteAllTextAsync(Arg.Is<string>(s => s.Contains("manifest.json")), Arg.Any<string>());
        await _fileSystem.Received(2).WriteAllTextAsync(Arg.Is<string>(s => s.Contains("metadata.json")), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateBackupAsync_EmptyProfile_ShouldCreateBackupWithZeroFiles()
    {
        // Arrange
        var profile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");
        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(true);
        _fileSystem.EnumerateFiles("C:\\Brave\\Default", "*", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        _fileSystem.DirectoryExists(Arg.Is<string>(s => s.Contains("profile"))).Returns(true);
        _fileSystem.EnumerateFiles(Arg.Is<string>(s => s.Contains("profile")), "*", SearchOption.AllDirectories)
            .Returns(Array.Empty<string>());

        // Act
        var result = await _service.CreateBackupAsync(profile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Metadata!.Status.Should().Be(BackupStatus.Completed);
        result.Statistics!.FileCount.Should().Be(0);
        result.Metadata.BackupSize.Should().Be(0L);
    }

    [Fact]
    public async Task CreateBackupAsync_MissingProfile_ShouldThrowProfileFolderNotFoundException()
    {
        // Arrange
        var profile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");
        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(false);

        // Act
        Func<Task> act = () => _service.CreateBackupAsync(profile);

        // Assert
        await act.Should().ThrowAsync<ProfileFolderNotFoundException>();
    }

    [Fact]
    public async Task ValidateBackupAsync_ValidBackup_ShouldReturnValid()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var profileDir = Path.Combine(backupDir, "profile");

        var manifest = new BackupManifest(
            BackupId: backupId,
            CreatedAt: DateTime.UtcNow,
            ProfileName: "Default",
            ProfilePath: "C:\\Brave\\Default",
            Version: "1.0",
            ProfileHash: GetExpectedHash(),
            FileCount: 1,
            BackupSize: 12L
        );

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.DirectoryExists(profileDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json"))
            .Returns(JsonSerializer.Serialize(manifest));

        _fileSystem.EnumerateFiles(profileDir, "*", SearchOption.AllDirectories)
            .Returns(new[] { Path.Combine(profileDir, "Preferences") });
        _fileSystem.FileExists(Path.Combine(profileDir, "Preferences")).Returns(true);
        _fileSystem.OpenRead(Arg.Any<string>()).Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("test content")));

        // Act
        var result = await _service.ValidateBackupAsync(backupId);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateBackupAsync_CorruptedManifest_ShouldReturnInvalid()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var profileDir = Path.Combine(backupDir, "profile");

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.DirectoryExists(profileDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json")).Returns("{ corrupted json }");

        // Act
        var result = await _service.ValidateBackupAsync(backupId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("corrupted") || e.Contains("Failed"));
    }

    [Fact]
    public async Task ValidateBackupAsync_MissingFiles_ShouldReturnInvalid()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var profileDir = Path.Combine(backupDir, "profile");

        var manifest = new BackupManifest(backupId, DateTime.UtcNow, "Default", "C:\\", "1.0", "hash", 5, 100);

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.DirectoryExists(profileDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json")).Returns(JsonSerializer.Serialize(manifest));

        // Actual files count is 2 (less than 5 in manifest)
        _fileSystem.EnumerateFiles(profileDir, "*", SearchOption.AllDirectories)
            .Returns(new[] { "file1", "file2" });

        // Act
        var result = await _service.ValidateBackupAsync(backupId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("File count mismatch"));
    }

    [Fact]
    public async Task ValidateBackupAsync_InvalidHash_ShouldReturnInvalid()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var profileDir = Path.Combine(backupDir, "profile");

        var manifest = new BackupManifest(backupId, DateTime.UtcNow, "Default", "C:\\", "1.0", "wrong_hash", 1, 12L);

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.DirectoryExists(profileDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json")).Returns(JsonSerializer.Serialize(manifest));

        _fileSystem.EnumerateFiles(profileDir, "*", SearchOption.AllDirectories)
            .Returns(new[] { Path.Combine(profileDir, "Preferences") });
        _fileSystem.FileExists(Path.Combine(profileDir, "Preferences")).Returns(true);
        _fileSystem.OpenRead(Arg.Any<string>()).Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("test content")));

        // Act
        var result = await _service.ValidateBackupAsync(backupId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Profile hash mismatch"));
    }

    [Fact]
    public async Task RestoreBackupAsync_SuccessfulRestore_ShouldRestoreCorrectly()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        var profileDir = Path.Combine(backupDir, "profile");

        var manifest = new BackupManifest(
            BackupId: backupId,
            CreatedAt: DateTime.UtcNow,
            ProfileName: "Default",
            ProfilePath: "C:\\Brave\\Default",
            Version: "1.0",
            ProfileHash: GetExpectedHash(),
            FileCount: 1,
            BackupSize: 12L
        );

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.DirectoryExists(profileDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json")).Returns(JsonSerializer.Serialize(manifest));

        _fileSystem.EnumerateFiles(profileDir, "*", SearchOption.AllDirectories)
            .Returns(new[] { Path.Combine(profileDir, "Preferences") });
        _fileSystem.FileExists(Path.Combine(profileDir, "Preferences")).Returns(true);
        _fileSystem.OpenRead(Arg.Any<string>()).Returns(x => new MemoryStream(Encoding.UTF8.GetBytes("test content")));
        _fileSystem.GetFileLength(Arg.Any<string>()).Returns(12L);

        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(true);

        // Act
        var result = await _service.RestoreBackupAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.FilesRestored.Should().Be(1);
        result.TotalBytesRestored.Should().Be(12L);

        // Verify that the existing profile was deleted/cleared before restore
        _fileSystem.Received(1).DeleteDirectory("C:\\Brave\\Default", true);
        _fileSystem.Received(1).CopyFile(Path.Combine(profileDir, "Preferences"), "C:\\Brave\\Default\\Preferences", true);
    }

    [Fact]
    public async Task RestoreBackupAsync_InvalidBackup_ShouldNotRestore()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        _fileSystem.DirectoryExists(Path.Combine(_backupsRoot, backupId.ToString())).Returns(false); // backup folder missing

        // Act
        var result = await _service.RestoreBackupAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Backup validation failed");
        _fileSystem.DidNotReceive().CopyFile(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task RestoreBackupAsync_CorruptedBackup_ShouldReturnFailure()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());

        _fileSystem.DirectoryExists(backupDir).Returns(true);
        _fileSystem.FileExists(Path.Combine(backupDir, "manifest.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(backupDir, "manifest.json")).Returns("{ corrupted json }");

        // Act
        var result = await _service.RestoreBackupAsync(backupId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Backup validation failed");
    }

    [Fact]
    public async Task GetBackupsAsync_NoBackups_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.DirectoryExists(_backupsRoot).Returns(false);

        // Act
        var result = await _service.GetBackupsAsync();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBackupsAsync_MultipleBackups_ShouldReturnMetadataList()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _fileSystem.DirectoryExists(_backupsRoot).Returns(true);
        _fileSystem.EnumerateDirectories(_backupsRoot)
            .Returns(new[] { Path.Combine(_backupsRoot, id1.ToString()), Path.Combine(_backupsRoot, id2.ToString()) });

        var meta1 = new BackupMetadata(id1, "Profile 1", "C:\\", DateTime.UtcNow, 100L, "1.0", BackupStatus.Completed);
        var meta2 = new BackupMetadata(id2, "Profile 2", "C:\\", DateTime.UtcNow, 200L, "1.0", BackupStatus.Failed);

        _fileSystem.FileExists(Path.Combine(_backupsRoot, id1.ToString(), "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(_backupsRoot, id1.ToString(), "metadata.json"))
            .Returns(JsonSerializer.Serialize(meta1));

        _fileSystem.FileExists(Path.Combine(_backupsRoot, id2.ToString(), "metadata.json")).Returns(true);
        _fileSystem.ReadAllTextAsync(Path.Combine(_backupsRoot, id2.ToString(), "metadata.json"))
            .Returns(JsonSerializer.Serialize(meta2));

        // Act
        var result = await _service.GetBackupsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Any(m => m.BackupId == id1 && m.Status == BackupStatus.Completed).Should().BeTrue();
        result.Any(m => m.BackupId == id2 && m.Status == BackupStatus.Failed).Should().BeTrue();
    }

    [Fact]
    public async Task DeleteBackupAsync_ExistingBackup_ShouldReturnTrueAndCallDelete()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        _fileSystem.DirectoryExists(backupDir).Returns(true);

        // Act
        var result = await _service.DeleteBackupAsync(backupId);

        // Assert
        result.Should().BeTrue();
        _fileSystem.Received(1).DeleteDirectory(backupDir, true);
    }

    [Fact]
    public async Task DeleteBackupAsync_MissingBackup_ShouldReturnFalse()
    {
        // Arrange
        var backupId = Guid.NewGuid();
        var backupDir = Path.Combine(_backupsRoot, backupId.ToString());
        _fileSystem.DirectoryExists(backupDir).Returns(false);

        // Act
        var result = await _service.DeleteBackupAsync(backupId);

        // Assert
        result.Should().BeFalse();
    }

    private string GetExpectedHash()
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var relativePathBytes = System.Text.Encoding.UTF8.GetBytes("Preferences");
        sha256.TransformBlock(relativePathBytes, 0, relativePathBytes.Length, relativePathBytes, 0);
        var contentBytes = System.Text.Encoding.UTF8.GetBytes("test content");
        sha256.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }

    // [Fact]
    private async Task DebugRealBackup()
    {
        var realFileSystem = new PhysicalFileSystem();
        var logger = NullLogger<BackupService>.Instance;
        var backupService = new BackupService(realFileSystem, logger, "C:\\Users\\Rahul_Muthuswamy\\AppData\\Roaming\\Aevor\\Backups\\DebugTemp");
        var profile = new BraveProfile(
            "Personel", 
            "Personel", 
            true, 
            true, 
            "C:\\Users\\Rahul_Muthuswamy\\AppData\\Local\\BraveSoftware\\Brave-Browser\\User Data\\Default"
        );
        Console.WriteLine("DEBUG: Enumerate files starting...");
        var files = realFileSystem.EnumerateFiles(profile.ProfilePath, "*", SearchOption.AllDirectories)
            .Where(f => !ShouldExcludeFile(f))
            .ToList();
        Console.WriteLine($"DEBUG: Enumerate completed. Found {files.Count} files.");
        
        // Run the backup
        var result = await backupService.CreateBackupAsync(profile);
        Console.WriteLine($"DEBUG: Backup completed! Success = {result.IsSuccess}");
    }

    private static bool ShouldExcludeFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) return true;
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
