using Microsoft.Extensions.Logging;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class ProfileAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_ShouldThrowCorruptedProfileException_WhenProfileDirectoryDoesNotExist()
    {
        var preferencesParser = Substitute.For<IPreferencesParser>();
        var securePreferencesParser = Substitute.For<ISecurePreferencesParser>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileAnalyzer>>();

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(false);

        var analyzer = new ProfileAnalyzer(preferencesParser, securePreferencesParser, registry, fileSystem, logger);

        var act = () => analyzer.AnalyzeAsync(profile);

        await act.Should().ThrowAsync<CorruptedProfileException>();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldReturnResultAndNoWarnings_WhenSettingsMatch()
    {
        var preferencesParser = Substitute.For<IPreferencesParser>();
        var securePreferencesParser = Substitute.For<ISecurePreferencesParser>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileAnalyzer>>();

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var theme = new ThemeInformation("theme1", "Dark", 123);
        var search = new SearchEngineInformation("Google", "google.com", "url");
        var sidebar = new SidebarConfiguration(true, "right");
        var verticalTabs = new VerticalTabsConfiguration(true);
        var extensions = new List<ExtensionInfo> { new("ext1", "Extension", "1.0", true) };

        var browserSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, extensions);

        preferencesParser.ParseAsync(Arg.Any<string>()).Returns(browserSettings);
        securePreferencesParser.ParseAsync(Arg.Any<string>()).Returns(browserSettings);

        var analyzer = new ProfileAnalyzer(preferencesParser, securePreferencesParser, registry, fileSystem, logger);

        var result = await analyzer.AnalyzeAsync(profile);

        result.Should().NotBeNull();
        result.ProfileName.Should().Be("Personal");
        result.Theme.ThemeId.Should().Be("theme1");
        result.Warnings.Should().BeEmpty();
        await registry.Received(1).SaveAsync();
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldProduceWarning_WhenExtensionIsMissingFromSecurePreferences()
    {
        var preferencesParser = Substitute.For<IPreferencesParser>();
        var securePreferencesParser = Substitute.For<ISecurePreferencesParser>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileAnalyzer>>();

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var theme = new ThemeInformation("theme1", "Dark", 123);
        var search = new SearchEngineInformation("Google", "google.com", "url");
        var sidebar = new SidebarConfiguration(true, "right");
        var verticalTabs = new VerticalTabsConfiguration(true);
        var prefExtensions = new List<ExtensionInfo> { new("ext1", "Extension One", "1.0", true) };
        var secExtensions = new List<ExtensionInfo>();

        var prefSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, prefExtensions);
        var secSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, secExtensions);

        preferencesParser.ParseAsync(Arg.Any<string>()).Returns(prefSettings);
        securePreferencesParser.ParseAsync(Arg.Any<string>()).Returns(secSettings);

        var analyzer = new ProfileAnalyzer(preferencesParser, securePreferencesParser, registry, fileSystem, logger);

        var result = await analyzer.AnalyzeAsync(profile);

        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Should().Contain("missing from Secure Preferences");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldProduceWarning_WhenExtensionStateMismatches()
    {
        var preferencesParser = Substitute.For<IPreferencesParser>();
        var securePreferencesParser = Substitute.For<ISecurePreferencesParser>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileAnalyzer>>();

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var theme = new ThemeInformation("theme1", "Dark", 123);
        var search = new SearchEngineInformation("Google", "google.com", "url");
        var sidebar = new SidebarConfiguration(true, "right");
        var verticalTabs = new VerticalTabsConfiguration(true);
        
        var prefExtensions = new List<ExtensionInfo> { new("ext1", "Extension One", "1.0", true) };
        var secExtensions = new List<ExtensionInfo> { new("ext1", "Extension One", "1.0", false) };

        var prefSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, prefExtensions);
        var secSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, secExtensions);

        preferencesParser.ParseAsync(Arg.Any<string>()).Returns(prefSettings);
        securePreferencesParser.ParseAsync(Arg.Any<string>()).Returns(secSettings);

        var analyzer = new ProfileAnalyzer(preferencesParser, securePreferencesParser, registry, fileSystem, logger);

        var result = await analyzer.AnalyzeAsync(profile);

        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Should().Contain("state mismatch");
    }

    [Fact]
    public async Task AnalyzeAsync_ShouldFallbackToSecurePreferencesExtensions_WhenPreferencesExtensionsIsEmpty()
    {
        var preferencesParser = Substitute.For<IPreferencesParser>();
        var securePreferencesParser = Substitute.For<ISecurePreferencesParser>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileAnalyzer>>();

        var profile = new BraveProfile("Default", "Personal", true, true, @"C:\Brave\Default");
        fileSystem.DirectoryExists(profile.ProfilePath).Returns(true);

        var theme = new ThemeInformation("theme1", "Dark", 123);
        var search = new SearchEngineInformation("Google", "google.com", "url");
        var sidebar = new SidebarConfiguration(true, "right");
        var verticalTabs = new VerticalTabsConfiguration(true);
        var prefExtensions = new List<ExtensionInfo>();
        var secExtensions = new List<ExtensionInfo> { new("ext1", "Extension One", "1.0", true) };

        var prefSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, prefExtensions);
        var secSettings = new BrowserSettings(theme, search, sidebar, verticalTabs, secExtensions);

        preferencesParser.ParseAsync(Arg.Any<string>()).Returns(prefSettings);
        securePreferencesParser.ParseAsync(Arg.Any<string>()).Returns(secSettings);

        var analyzer = new ProfileAnalyzer(preferencesParser, securePreferencesParser, registry, fileSystem, logger);

        var result = await analyzer.AnalyzeAsync(profile);

        result.InstalledExtensions.Should().ContainSingle().Which.Id.Should().Be("ext1");
        result.ExtensionCount.Should().Be(1);
    }
}
