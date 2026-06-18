using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class PreferencesParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldThrowPreferencesFileNotFoundException_WhenFileDoesNotExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new PreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Preferences";

        fileSystem.FileExists(path).Returns(false);

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<PreferencesFileNotFoundException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldThrowInvalidPreferencesJsonException_WhenJsonIsMalformed()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new PreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Preferences";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns("{ malformed }");

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<InvalidPreferencesJsonException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldThrowProfileAccessDeniedException_WhenUnauthorizedAccess()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new PreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Preferences";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).ThrowsAsync(new UnauthorizedAccessException());

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<ProfileAccessDeniedException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnParsedSettings_WhenJsonIsValid()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new PreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Preferences";

        var validJson = @"{
            ""extensions"": {
                ""theme"": {
                    ""id"": ""theme123""
                },
                ""settings"": {
                    ""ext1"": {
                        ""manifest"": {
                            ""name"": ""My Extension"",
                            ""version"": ""1.0.0""
                        },
                        ""state"": 1
                    }
                }
            },
            ""brave"": {
                ""colors"": {
                    ""theme_mode"": 2
                },
                ""sidebar"": {
                    ""show"": true,
                    ""position"": ""right""
                },
                ""tabs"": {
                    ""use_vertical_tabs"": true
                }
            },
            ""default_search_provider"": {
                ""name"": ""Google"",
                ""keyword"": ""google.com"",
                ""search_url"": ""https://google.com""
            }
        }";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns(validJson);

        var result = await parser.ParseAsync(path);

        result.Should().NotBeNull();
        result.Theme.ThemeId.Should().Be("theme123");
        result.Theme.SystemThemeMode.Should().Be("Dark");
        result.SearchEngine.Name.Should().Be("Google");
        result.Sidebar.ShowSidebar.Should().BeTrue();
        result.Sidebar.Position.Should().Be("right");
        result.VerticalTabs.UseVerticalTabs.Should().BeTrue();
        result.Extensions.Should().HaveCount(1);
        result.Extensions[0].Id.Should().Be("ext1");
        result.Extensions[0].Name.Should().Be("My Extension");
        result.Extensions[0].Version.Should().Be("1.0.0");
        result.Extensions[0].IsEnabled.Should().BeTrue();
    }
}
