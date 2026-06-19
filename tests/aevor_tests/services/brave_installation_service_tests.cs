using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class BraveInstallationServiceTests
{
    [Fact]
    public void IsInstalled_ShouldReturnTrue_WhenDirectoryExists()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var service = new BraveInstallationService(fileSystem);
        var expectedPath = service.GetUserDataPath();

        fileSystem.DirectoryExists(expectedPath).Returns(true);

        var result = service.IsInstalled();

        result.Should().BeTrue();
    }

    [Fact]
    public void IsInstalled_ShouldReturnFalse_WhenDirectoryDoesNotExist()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var service = new BraveInstallationService(fileSystem);
        var expectedPath = service.GetUserDataPath();

        fileSystem.DirectoryExists(expectedPath).Returns(false);

        var result = service.IsInstalled();

        result.Should().BeFalse();
    }

    [Fact]
    public void GetUserDataPath_ShouldReturnExpectedPathOnWindows()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var service = new BraveInstallationService(fileSystem);

        var path = service.GetUserDataPath();

        path.Should().Contain(@"BraveSoftware\Brave-Browser\User Data");
        path.Should().StartWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    [Fact]
    public void GetUserDataPath_ShouldReturnCustomPath_WhenSettingsFileExists()
    {
        var fileSystem = Substitute.For<IFileSystem>();
        var service = new BraveInstallationService(fileSystem);
        var settingsPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor",
            "settings.json"
        );

        fileSystem.FileExists(settingsPath).Returns(true);
        fileSystem.ReadAllText(settingsPath).Returns("{\"BraveUserDataPath\": \"C:\\\\Custom\\\\Path\"}");
        fileSystem.DirectoryExists("C:\\Custom\\Path").Returns(true);

        var path = service.GetUserDataPath();

        path.Should().Be("C:\\Custom\\Path");
    }
}
