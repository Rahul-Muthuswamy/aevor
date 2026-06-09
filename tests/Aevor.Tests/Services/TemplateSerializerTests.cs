using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class TemplateSerializerTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly TemplateSerializer _serializer;

    public TemplateSerializerTests()
    {
        _serializer = new TemplateSerializer(_fileSystem);
    }

    private AevorTemplate CreateValidTemplate()
    {
        return new AevorTemplate(
            Metadata: new TemplateMetadata(
                Name: "My Template",
                Description: "Description text",
                CreatedTimestamp: new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc),
                TemplateVersion: TemplateVersion.V1_0,
                SourceBrowser: "Brave",
                SourceBrowserVersion: "1.0.0",
                SourceProfileName: "Default",
                GeneratorVersion: "1.0.0"
            ),
            Settings: new TemplateSettings(
                Theme: new ThemeInformation("themeId", "Dark", 12345),
                SearchEngine: new SearchEngineInformation("Brave Search", "brave", "https://search.brave.com"),
                Sidebar: new SidebarConfiguration(true, "Right"),
                VerticalTabs: new VerticalTabsConfiguration(true),
                BrowserPreferences: new Dictionary<string, object>()
            ),
            Extensions: new List<ExtensionInfo>
            {
                new("ext1", "Extension One", "1.0.0", true)
            },
            Assets: new TemplateAssets(null, null, new Dictionary<string, string>()),
            Warnings: new List<TemplateWarning>(),
            ExcludedArtifacts: new List<ExcludedArtifact>()
        );
    }

    [Fact]
    public void Serialize_ValidTemplate_ShouldReturnValidJson()
    {
        // Arrange
        var template = CreateValidTemplate();

        // Act
        var json = _serializer.Serialize(template);

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"Name\": \"My Template\"");
        json.Should().Contain("\"TemplateVersion\": \"1.0\"");
    }

    [Fact]
    public void Deserialize_ValidJson_ShouldReturnTemplate()
    {
        // Arrange
        var json = @"{
            ""Metadata"": {
                ""Name"": ""My Template"",
                ""Description"": ""Description text"",
                ""CreatedTimestamp"": ""2026-06-09T12:00:00Z"",
                ""TemplateVersion"": ""1.0"",
                ""SourceBrowser"": ""Brave"",
                ""SourceBrowserVersion"": ""1.0.0"",
                ""SourceProfileName"": ""Default"",
                ""GeneratorVersion"": ""1.0.0""
            },
            ""Settings"": {
                ""Theme"": {
                    ""ThemeId"": ""themeId"",
                    ""SystemThemeMode"": ""Dark"",
                    ""ThemeColor"": 12345
                },
                ""SearchEngine"": {
                    ""Name"": ""Brave Search"",
                    ""Keyword"": ""brave"",
                    ""SearchUrl"": ""https://search.brave.com""
                },
                ""Sidebar"": {
                    ""ShowSidebar"": true,
                    ""Position"": ""Right""
                },
                ""VerticalTabs"": {
                    ""UseVerticalTabs"": true
                },
                ""BrowserPreferences"": {}
            },
            ""Extensions"": [
                {
                    ""Id"": ""ext1"",
                    ""Name"": ""Extension One"",
                    ""Version"": ""1.0.0"",
                    ""IsEnabled"": true
                }
            ],
            ""Assets"": {
                ""Wallpaper"": null,
                ""Icon"": null,
                ""FutureAssets"": {}
            },
            ""Warnings"": [],
            ""ExcludedArtifacts"": []
        }";

        // Act
        var template = _serializer.Deserialize(json);

        // Assert
        template.Should().NotBeNull();
        template.Metadata.Name.Should().Be("My Template");
        template.Metadata.TemplateVersion.Should().Be(TemplateVersion.V1_0);
        template.Settings.Sidebar.ShowSidebar.Should().BeTrue();
        template.Extensions.Should().ContainSingle().Which.Id.Should().Be("ext1");
    }

    [Fact]
    public void Deserialize_InvalidJson_ShouldThrowTemplateSerializationException()
    {
        // Arrange
        var malformedJson = "{ invalid json }";

        // Act
        Action act = () => _serializer.Deserialize(malformedJson);

        // Assert
        act.Should().Throw<TemplateSerializationException>();
    }

    [Fact]
    public void Deserialize_MissingVersionMetadata_ShouldThrowTemplateVersionException()
    {
        // Arrange
        var jsonWithoutVersion = @"{
            ""Metadata"": {
                ""Name"": ""No Version Template""
            }
        }";

        // Act
        Action act = () => _serializer.Deserialize(jsonWithoutVersion);

        // Assert
        act.Should().Throw<TemplateVersionException>().WithMessage("*version*");
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_ShouldThrowTemplateVersionException()
    {
        // Arrange
        var jsonWithUnsupportedVersion = @"{
            ""Metadata"": {
                ""Name"": ""New Version Template"",
                ""TemplateVersion"": ""2.0""
            }
        }";

        // Act
        Action act = () => _serializer.Deserialize(jsonWithUnsupportedVersion);

        // Assert
        act.Should().Throw<TemplateVersionException>().WithMessage("*version*2.0*");
    }

    [Fact]
    public async Task SaveToFileAsync_ShouldWriteToFile()
    {
        // Arrange
        var template = CreateValidTemplate();
        var path = "C:\\path\\template.aevor";
        _fileSystem.DirectoryExists("C:\\path").Returns(true);

        // Act
        await _serializer.SaveToFileAsync(path, template);

        // Assert
        await _fileSystem.Received(1).WriteAllTextAsync(path, Arg.Any<string>());
    }

    [Fact]
    public async Task LoadFromFileAsync_ShouldReadAndDeserialize()
    {
        // Arrange
        var path = "C:\\path\\template.aevor";
        var json = @"{
            ""Metadata"": {
                ""Name"": ""File Template"",
                ""Description"": ""Desc"",
                ""CreatedTimestamp"": ""2026-06-09T12:00:00Z"",
                ""TemplateVersion"": ""1.0"",
                ""SourceBrowser"": ""Brave"",
                ""SourceBrowserVersion"": ""1.0.0"",
                ""SourceProfileName"": ""Default"",
                ""GeneratorVersion"": ""1.0.0""
            },
            ""Settings"": {
                ""Theme"": { ""ThemeId"": null, ""SystemThemeMode"": null, ""ThemeColor"": null },
                ""SearchEngine"": { ""Name"": null, ""Keyword"": null, ""SearchUrl"": null },
                ""Sidebar"": { ""ShowSidebar"": false, ""Position"": null },
                ""VerticalTabs"": { ""UseVerticalTabs"": false },
                ""BrowserPreferences"": {}
            },
            ""Extensions"": [],
            ""Assets"": { ""Wallpaper"": null, ""Icon"": null, ""FutureAssets"": null },
            ""Warnings"": [],
            ""ExcludedArtifacts"": []
        }";

        _fileSystem.FileExists(path).Returns(true);
        _fileSystem.ReadAllTextAsync(path).Returns(json);

        // Act
        var template = await _serializer.LoadFromFileAsync(path);

        // Assert
        template.Should().NotBeNull();
        template.Metadata.Name.Should().Be("File Template");
    }
}
