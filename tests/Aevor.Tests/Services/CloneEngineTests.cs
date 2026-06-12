using System;
using System.Collections.Generic;
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

public class CloneEngineTests
{
    private readonly IProfileDiscoveryService _profileDiscoveryService = Substitute.For<IProfileDiscoveryService>();
    private readonly IProfileAnalyzer _profileAnalyzer = Substitute.For<IProfileAnalyzer>();
    private readonly ISecurityScanner _securityScanner = Substitute.For<ISecurityScanner>();
    private readonly ITemplateBuilder _templateBuilder = Substitute.For<ITemplateBuilder>();
    private readonly IBackupService _backupService = Substitute.For<IBackupService>();
    private readonly IProfileCreator _profileCreator = Substitute.For<IProfileCreator>();
    private readonly ITemplateApplier _templateApplier = Substitute.For<ITemplateApplier>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly NullLogger<CloneEngine> _logger = NullLogger<CloneEngine>.Instance;
    private readonly CloneEngine _engine;

    public CloneEngineTests()
    {
        _engine = new CloneEngine(
            _profileDiscoveryService,
            _profileAnalyzer,
            _securityScanner,
            _templateBuilder,
            _backupService,
            _profileCreator,
            _templateApplier,
            _fileSystem,
            _logger
        );
    }

    [Fact]
    public async Task CloneProfileAsync_ValidRequest_ShouldRunFullWorkflowAndReturnSuccess()
    {
        // Arrange
        var request = new CloneRequest("Default", "Cloned Profile");
        
        var sourceProfile = new BraveProfile("Default", "Default", true, true, "C:\\Brave\\Default");
        _profileDiscoveryService.GetProfilesAsync().Returns(new List<BraveProfile> { sourceProfile });

        var theme = new ThemeInformation("id", "Dark", 0);
        var search = new SearchEngineInformation("Brave", "brave", "url");
        var sidebar = new SidebarConfiguration(true, "Right");
        var tabs = new VerticalTabsConfiguration(false);
        var extensions = new List<ExtensionInfo>();

        var analysisResult = new ProfileAnalysisResult("Default", "C:\\", theme, search, sidebar, tabs, extensions, 0, DateTime.UtcNow, new List<string>(), new List<string>());
        _profileAnalyzer.AnalyzeAsync(sourceProfile).Returns(analysisResult);

        var scanResult = new SecurityScanResult("Default", "C:\\", DateTime.UtcNow, 0, RiskLevel.Low, new List<SecurityFinding>(), false, false, false, false, false, false, true);
        _securityScanner.ScanAsync(sourceProfile).Returns(scanResult);

        var tempTemplate = new AevorTemplate(
            Metadata: new TemplateMetadata("Clone", "Desc", DateTime.UtcNow, TemplateVersion.V1_0, "Brave", "1.0", "Default", "1.0"),
            Settings: new TemplateSettings(theme, search, sidebar, tabs, new Dictionary<string, object>()),
            Extensions: extensions,
            Assets: new TemplateAssets(null, null, null),
            Warnings: new List<TemplateWarning>(),
            ExcludedArtifacts: new List<ExcludedArtifact>()
        );
        _templateBuilder.Build(analysisResult, scanResult, "Cloned Profile", Arg.Any<string>(), Arg.Any<string>()).Returns(tempTemplate);

        var backupMeta = new BackupMetadata(Guid.NewGuid(), "Default", "C:\\", DateTime.UtcNow, 100L, "1.0", BackupStatus.Completed);
        _backupService.CreateBackupAsync(sourceProfile).Returns(new BackupResult(true, backupMeta, null));

        var destProfile = new BraveProfile("Profile 1", "Cloned Profile", false, false, "C:\\Brave\\Profile 1");
        var registration = new ProfileRegistrationInfo("Profile 1", "Cloned Profile", "C:\\Brave\\Profile 1", DateTime.UtcNow);
        _profileCreator.CreateProfileAsync(Arg.Any<ProfileCreationRequest>()).Returns(new ProfileCreationResult(true, destProfile, null, registration));

        _templateApplier.ApplyTemplateAsync(tempTemplate, destProfile).Returns(new TemplateApplicationResult(true, null, backupMeta.BackupId));
        _profileCreator.ValidateProfileAsync("Profile 1").Returns(new ProfileValidationResult(true, new List<string>(), new List<string>()));

        // Act
        var result = await _engine.CloneProfileAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Report.Should().NotBeNull();
        result.Report!.SourceProfile.FolderName.Should().Be("Default");
        result.Report.DestinationProfile.FolderName.Should().Be("Profile 1");

        // Verify order of calls
        await _profileAnalyzer.Received(1).AnalyzeAsync(sourceProfile);
        await _securityScanner.Received(1).ScanAsync(sourceProfile);
        await _backupService.Received(1).CreateBackupAsync(sourceProfile);
        await _profileCreator.Received(1).CreateProfileAsync(Arg.Is<ProfileCreationRequest>(r => r.ProfileName == "Cloned Profile"));
        await _templateApplier.Received(1).ApplyTemplateAsync(tempTemplate, destProfile);
    }
}
