using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class LocalStateParserTests
{
    [Fact]
    public async Task ParseAsync_ShouldThrowMissingLocalStateFileException_WhenFileDoesNotExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var parser = new LocalStateParser(fileSystem);
        var path = "C:\\path\\to\\Local State";

        fileSystem.FileExists(path).Returns(false);

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<MissingLocalStateFileException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldThrowInvalidLocalStateJsonException_WhenJsonIsMalformed()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var parser = new LocalStateParser(fileSystem);
        var path = "C:\\path\\to\\Local State";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns("{ malformed json }");

        var act = () => parser.ParseAsync(path);

        await act.Should().ThrowAsync<InvalidLocalStateJsonException>();
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnParsedMetadata_WhenJsonIsValid()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var parser = new LocalStateParser(fileSystem);
        var path = "C:\\path\\to\\Local State";

        var validJson = @"{
            ""profile"": {
                ""info_cache"": {
                    ""Default"": {
                        ""name"": ""Personal Profile"",
                        ""avatar_icon"": ""chrome://theme/IDR_PROFILE_AVATAR_0""
                    },
                    ""Profile 1"": {
                        ""name"": ""Work Profile"",
                        ""avatar_icon"": ""chrome://theme/IDR_PROFILE_AVATAR_1""
                    }
                },
                ""last_used"": ""Profile 1""
            }
        }";

        fileSystem.FileExists(path).Returns(true);
        fileSystem.ReadAllTextAsync(path).Returns(validJson);

        var result = await parser.ParseAsync(path);

        result.Should().NotBeNull();
        result.Profile.LastUsed.Should().Be("Profile 1");
        result.Profile.InfoCache.Should().HaveCount(2);
        result.Profile.InfoCache["Default"].Name.Should().Be("Personal Profile");
        result.Profile.InfoCache["Default"].AvatarIcon.Should().Be("chrome://theme/IDR_PROFILE_AVATAR_0");
        result.Profile.InfoCache["Profile 1"].Name.Should().Be("Work Profile");
    }
}
