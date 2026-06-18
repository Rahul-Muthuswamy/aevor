using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class TemplateBuilderTests
{
    private readonly TemplateBuilder _builder = new();

    [Fact]
    public void Build_ValidTemplate_ShouldMapCorrectly()
    {
        // Arrange
        var theme = new ThemeInformation("themeId", "Dark", 12345);
        var search = new SearchEngineInformation("Brave Search", "brave", "https://search.brave.com");
        var sidebar = new SidebarConfiguration(true, "Right");
        var tabs = new VerticalTabsConfiguration(true);
        var extensions = new List<ExtensionInfo>
        {
            new("ext1", "Extension One", "1.0.0", true)
        };

        var analysisResult = new ProfileAnalysisResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            Theme: theme,
            SearchEngine: search,
            Sidebar: sidebar,
            VerticalTabs: tabs,
            InstalledExtensions: extensions,
            ExtensionCount: 1,
            AnalysisTimestamp: DateTime.UtcNow,
            Warnings: new List<string>(),
            Errors: new List<string>()
        );

        var scanResult = new SecurityScanResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            ScanTimestamp: DateTime.UtcNow,
            RiskScore: 0,
            RiskLevel: RiskLevel.Low,
            Findings: new List<SecurityFinding>(),
            HasPasswords: false,
            HasCookies: false,
            HasWalletData: false,
            HasAutofillData: false,
            HasSessions: false,
            HasExtensionStorage: false,
            ExportSafe: true
        );

        // Act
        var template = _builder.Build(analysisResult, scanResult, "My Template", "Description text", "2.0.0");

        // Assert
        template.Should().NotBeNull();
        template.Metadata.Name.Should().Be("My Template");
        template.Metadata.Description.Should().Be("Description text");
        template.Metadata.GeneratorVersion.Should().Be("2.0.0");
        template.Metadata.TemplateVersion.Should().Be(TemplateVersion.V1_0);
        template.Metadata.SourceProfileName.Should().Be("TestProfile");

        template.Settings.Theme.Should().Be(theme);
        template.Settings.SearchEngine.Should().Be(search);
        template.Settings.Sidebar.Should().Be(sidebar);
        template.Settings.VerticalTabs.Should().Be(tabs);
        template.Settings.BrowserPreferences.Should().BeEmpty();

        template.Extensions.Should().ContainSingle().Which.Id.Should().Be("ext1");
        template.ExcludedArtifacts.Should().BeEmpty();
        template.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Build_WithExclusions_ShouldRecordExcludedArtifactsAndWarnings()
    {
        // Arrange
        var theme = new ThemeInformation(null, null, null);
        var search = new SearchEngineInformation(null, null, null);
        var sidebar = new SidebarConfiguration(false, null);
        var tabs = new VerticalTabsConfiguration(false);
        var extensions = new List<ExtensionInfo>();

        var analysisResult = new ProfileAnalysisResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            Theme: theme,
            SearchEngine: search,
            Sidebar: sidebar,
            VerticalTabs: tabs,
            InstalledExtensions: extensions,
            ExtensionCount: 0,
            AnalysisTimestamp: DateTime.UtcNow,
            Warnings: new List<string>(),
            Errors: new List<string>()
        );

        var findings = new List<SecurityFinding>
        {
            new("Saved Passwords", "Credentials", SecuritySeverity.Critical, "Saved passwords database containing credentials detected.", "C:\\Profile\\Login Data"),
            new("Session Cookies", "Cookies", SecuritySeverity.High, "Active session cookies database detected.", "C:\\Profile\\Cookies")
        };

        var scanResult = new SecurityScanResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            ScanTimestamp: DateTime.UtcNow,
            RiskScore: 60,
            RiskLevel: RiskLevel.High,
            Findings: findings,
            HasPasswords: true,
            HasCookies: true,
            HasWalletData: false,
            HasAutofillData: false,
            HasSessions: false,
            HasExtensionStorage: false,
            ExportSafe: false
        );

        // Act
        var template = _builder.Build(analysisResult, scanResult, "Secure Template", "Description");

        // Assert
        template.ExcludedArtifacts.Should().HaveCount(2);
        template.ExcludedArtifacts[0].Name.Should().Be("Saved Passwords");
        template.ExcludedArtifacts[0].Path.Should().Be("C:\\Profile\\Login Data");
        template.ExcludedArtifacts[0].Reason.Should().Be("Saved passwords database containing credentials detected.");

        template.ExcludedArtifacts[1].Name.Should().Be("Session Cookies");
        template.ExcludedArtifacts[1].Path.Should().Be("C:\\Profile\\Cookies");

        // Warnings should include exclusions and safety check warning
        template.Warnings.Should().HaveCount(3);
        template.Warnings.Any(w => w.Code == "SEC_EXCLUSION" && w.Message.Contains("Saved Passwords")).Should().BeTrue();
        template.Warnings.Any(w => w.Code == "SEC_EXCLUSION" && w.Message.Contains("Session Cookies")).Should().BeTrue();
        template.Warnings.Any(w => w.Code == "SEC_EXPORT_UNSAFE").Should().BeTrue();
    }

    [Fact]
    public void Build_WithWarnings_ShouldRecordWarnings()
    {
        // Arrange
        var theme = new ThemeInformation(null, null, null);
        var search = new SearchEngineInformation(null, null, null);
        var sidebar = new SidebarConfiguration(false, null);
        var tabs = new VerticalTabsConfiguration(false);
        var extensions = new List<ExtensionInfo>();

        var analysisResult = new ProfileAnalysisResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            Theme: theme,
            SearchEngine: search,
            Sidebar: sidebar,
            VerticalTabs: tabs,
            InstalledExtensions: extensions,
            ExtensionCount: 0,
            AnalysisTimestamp: DateTime.UtcNow,
            Warnings: new List<string> { "Analysis Warn 1", "Analysis Warn 2" },
            Errors: new List<string> { "Analysis Err 1" }
        );

        var scanResult = new SecurityScanResult(
            ProfileName: "TestProfile",
            ProfilePath: "C:\\Profile",
            ScanTimestamp: DateTime.UtcNow,
            RiskScore: 0,
            RiskLevel: RiskLevel.Low,
            Findings: new List<SecurityFinding>(),
            HasPasswords: false,
            HasCookies: false,
            HasWalletData: false,
            HasAutofillData: false,
            HasSessions: false,
            HasExtensionStorage: false,
            ExportSafe: true
        );

        // Act
        var template = _builder.Build(analysisResult, scanResult, "Template", "Desc");

        // Assert
        template.Warnings.Should().HaveCount(3);
        template.Warnings.Where(w => w.Code == "ANALYSIS_WARNING").Should().HaveCount(2);
        template.Warnings.Where(w => w.Code == "ANALYSIS_ERROR").Should().HaveCount(1);
    }

    [Fact]
    public void Build_EmptyAnalysisResult_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _builder.Build(null!, new SecurityScanResult("P", "Path", DateTime.UtcNow, 0, RiskLevel.Low, new List<SecurityFinding>(), false, false, false, false, false, false, true), "T", "D");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("analysisResult");
    }

    [Fact]
    public void Build_EmptySecurityResult_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => _builder.Build(new ProfileAnalysisResult("P", "Path", new ThemeInformation(null, null, null), new SearchEngineInformation(null, null, null), new SidebarConfiguration(false, null), new VerticalTabsConfiguration(false), new List<ExtensionInfo>(), 0, DateTime.UtcNow, new List<string>(), new List<string>()), null!, "T", "D");

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("scanResult");
    }
}
