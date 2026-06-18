using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public class TemplateApplierTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly ITemplateValidator _templateValidator = Substitute.For<ITemplateValidator>();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly NullLogger<TemplateApplier> _logger = NullLogger<TemplateApplier>.Instance;
    private readonly TemplateApplier _applier;

    public TemplateApplierTests()
    {
        _applier = new TemplateApplier(_fileSystem, _templateValidator, _backupService, _logger);
    }

    private AevorTemplate CreateValidTemplate()
    {
        return new AevorTemplate(
            Metadata: new TemplateMetadata("T1", "Desc", DateTime.UtcNow, TemplateVersion.V1_0, "Brave", "1.0", "Def", "1.0"),
            Settings: new TemplateSettings(
                Theme: new ThemeInformation("theme_id", "Dark", 123),
                SearchEngine: new SearchEngineInformation("Brave", "brave", "https://search"),
                Sidebar: new SidebarConfiguration(true, "Left"),
                VerticalTabs: new VerticalTabsConfiguration(true),
                BrowserPreferences: new Dictionary<string, object>()
            ),
            Extensions: new List<ExtensionInfo>
            {
                new("ext_1", "Tampermonkey", "1.0", true)
            },
            Assets: new TemplateAssets(null, null, new Dictionary<string, string>()),
            Warnings: new List<TemplateWarning>(),
            ExcludedArtifacts: new List<ExcludedArtifact>()
        );
    }

    [Fact]
    public async Task ApplyTemplateAsync_ValidRequest_ShouldCreateBackupAndApplySettings()
    {
        // Arrange
        var template = CreateValidTemplate();
        var profile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");

        _templateValidator.Validate(template).Returns(new TemplateValidationResult(true, new List<TemplateError>(), new List<TemplateWarning>()));
        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(true);
        _fileSystem.FileExists("C:\\Brave\\Default\\Preferences").Returns(true);
        _fileSystem.FileExists("C:\\Brave\\Default\\Secure Preferences").Returns(true);

        _fileSystem.ReadAllTextAsync("C:\\Brave\\Default\\Preferences").Returns("{}");
        _fileSystem.ReadAllTextAsync("C:\\Brave\\Default\\Secure Preferences").Returns("{}");

        var backupMeta = new BackupMetadata(Guid.NewGuid(), "Default", "C:\\Brave\\Default", DateTime.UtcNow, 100, "1.0", BackupStatus.Completed);
        _backupService.CreateBackupAsync(profile).Returns(new BackupResult(true, backupMeta, null));

        // Act
        var result = await _applier.ApplyTemplateAsync(template, profile);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.BackupId.Should().Be(backupMeta.BackupId);

        // Verify backup called
        await _backupService.Received(1).CreateBackupAsync(profile);

        // Verify write-backs called for preferences and secure preferences
        await _fileSystem.Received(1).WriteAllTextAsync("C:\\Brave\\Default\\Preferences", Arg.Is<string>(s => s.Contains("brave") && s.Contains("theme_mode") && s.Contains("Tampermonkey")));
        await _fileSystem.Received(1).WriteAllTextAsync("C:\\Brave\\Default\\Secure Preferences", Arg.Is<string>(s => s.Contains("Tampermonkey")));
    }

    [Fact]
    public async Task ApplyTemplateAsync_WriteFail_ShouldTriggerRollback()
    {
        // Arrange
        var template = CreateValidTemplate();
        var profile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");

        _templateValidator.Validate(template).Returns(new TemplateValidationResult(true, new List<TemplateError>(), new List<TemplateWarning>()));
        _fileSystem.DirectoryExists("C:\\Brave\\Default").Returns(true);
        _fileSystem.FileExists("C:\\Brave\\Default\\Preferences").Returns(true);
        _fileSystem.FileExists("C:\\Brave\\Default\\Secure Preferences").Returns(true);

        _fileSystem.ReadAllTextAsync("C:\\Brave\\Default\\Preferences").Returns("{}");
        _fileSystem.ReadAllTextAsync("C:\\Brave\\Default\\Secure Preferences").Returns("{}");

        var backupId = Guid.NewGuid();
        var backupMeta = new BackupMetadata(backupId, "Default", "C:\\Brave\\Default", DateTime.UtcNow, 100, "1.0", BackupStatus.Completed);
        _backupService.CreateBackupAsync(profile).Returns(new BackupResult(true, backupMeta, null));

        // Simulate file system exception during writing Preferences
        _fileSystem.WriteAllTextAsync("C:\\Brave\\Default\\Preferences", Arg.Any<string>())
            .Returns(x => throw new IOException("Disk Full"));

        // Act
        Func<Task> act = () => _applier.ApplyTemplateAsync(template, profile);

        // Assert
        await act.Should().ThrowAsync<TemplateApplicationException>();

        // Verify that restore/rollback was called
        await _backupService.Received(1).RestoreBackupAsync(backupId);
    }
}
