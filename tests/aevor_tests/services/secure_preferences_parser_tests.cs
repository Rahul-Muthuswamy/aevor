using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class SecurePreferencesParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldThrowSecurePreferencesFileNotFoundException_WhenFileDoesNotExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new SecurePreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Secure Preferences";

        fileSystem.FileExists(path).Returns(false);

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<SecurePreferencesFileNotFoundException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldThrowInvalidSecurePreferencesJsonException_WhenJsonIsMalformed()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new SecurePreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Secure Preferences";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns("{ malformed }");

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<InvalidSecurePreferencesJsonException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnParsedSettings_WhenJsonIsValid()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new SecurePreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Secure Preferences";

        var validJson = @"{
            ""extensions"": {
                ""settings"": {
                    ""ext1"": {
                        ""manifest"": {
                            ""name"": ""My Extension"",
                            ""version"": ""1.0.0""
                        },
                        ""state"": 1
                    }
                }
            }
        }";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns(validJson);

        var result = await parser.ParseAsync(path);

        result.Should().NotBeNull();
        result.Extensions.Should().HaveCount(1);
        result.Extensions[0].Id.Should().Be("ext1");
        result.Extensions[0].Name.Should().Be("My Extension");
        result.Extensions[0].Version.Should().Be("1.0.0");
        result.Extensions[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ShouldDefaultExtensionToEnabled_WhenStateIsMissing()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var registry = Substitute.For<IDiscoveredSettingRegistry>();
        var parser = new SecurePreferencesParser(fileSystem, registry);
        var path = "C:\\path\\to\\Secure Preferences";

        var validJson = @"{
            ""extensions"": {
                ""settings"": {
                    ""ext1"": {
                        ""manifest"": {
                            ""name"": ""My Extension"",
                            ""version"": ""1.0.0""
                        }
                    }
                }
            }
        }";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns(validJson);

        var result = await parser.ParseAsync(path);

        result.Should().NotBeNull();
        result.Extensions.Should().HaveCount(1);
        result.Extensions[0].IsEnabled.Should().BeTrue();
    }
}
