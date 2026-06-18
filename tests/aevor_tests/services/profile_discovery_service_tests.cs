using Microsoft.Extensions.Logging;
using FluentAssertions;
using NSubstitute;
using Xunit;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;
using Aevor.Core.Exceptions;
using Aevor.Infrastructure.Services;

namespace Aevor.Tests.Services;

public class ProfileDiscoveryServiceTests
{
    [Fact]
    public async Task GetProfilesAsync_ShouldThrowBraveNotInstalledException_WhenBraveNotInstalled()
    {
        var installService = Substitute.For<IBraveInstallationService>();
        var parser = Substitute.For<ILocalStateParser>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileDiscoveryService>>();

        installService.IsInstalled().Returns(false);

        var service = new ProfileDiscoveryService(installService, parser, fileSystem, logger);

        var act = () => service.GetProfilesAsync();

        await act.Should().ThrowAsync<BraveNotInstalledException>();
    }

    [Fact]
    public async Task GetProfilesAsync_ShouldDiscoverProfiles_WhenDirectoriesExist()
    {
        var installService = Substitute.For<IBraveInstallationService>();
        var parser = Substitute.For<ILocalStateParser>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileDiscoveryService>>();

        installService.IsInstalled().Returns(true);
        installService.GetUserDataPath().Returns(@"C:\Brave\User Data");

        var localState = new LocalStateMetadata(
            new LocalStateProfileData(
                new Dictionary<string, LocalStateProfileMetadata>
                {
                    { "Default", new LocalStateProfileMetadata("Personal", "avatar1") },
                    { "Profile 1", new LocalStateProfileMetadata("Work", "avatar2") }
                },
                "Profile 1"
            )
        );

        parser.ParseAsync(@"C:\Brave\User Data\Local State").Returns(localState);

        fileSystem.DirectoryExists(@"C:\Brave\User Data\Default").Returns(true);
        fileSystem.DirectoryExists(@"C:\Brave\User Data\Profile 1").Returns(true);

        var service = new ProfileDiscoveryService(installService, parser, fileSystem, logger);

        var profiles = await service.GetProfilesAsync();

        profiles.Should().HaveCount(2);

        var defaultProfile = profiles.Find(p => p.FolderName == "Default");
        defaultProfile.Should().NotBeNull();
        defaultProfile!.DisplayName.Should().Be("Personal");
        defaultProfile.IsDefault.Should().BeTrue();
        defaultProfile.IsLastUsed.Should().BeFalse();
        defaultProfile.ProfilePath.Should().Be(@"C:\Brave\User Data\Default");

        var workProfile = profiles.Find(p => p.FolderName == "Profile 1");
        workProfile.Should().NotBeNull();
        workProfile!.DisplayName.Should().Be("Work");
        workProfile.IsDefault.Should().BeFalse();
        workProfile.IsLastUsed.Should().BeTrue();
        workProfile.ProfilePath.Should().Be(@"C:\Brave\User Data\Profile 1");
    }

    [Fact]
    public async Task GetProfilesAsync_ShouldSkipProfile_WhenDirectoryDoesNotExist()
    {
        var installService = Substitute.For<IBraveInstallationService>();
        var parser = Substitute.For<ILocalStateParser>();
        var fileSystem = Substitute.For<IFileSystem>();
        var logger = Substitute.For<ILogger<ProfileDiscoveryService>>();

        installService.IsInstalled().Returns(true);
        installService.GetUserDataPath().Returns(@"C:\Brave\User Data");

        var localState = new LocalStateMetadata(
            new LocalStateProfileData(
                new Dictionary<string, LocalStateProfileMetadata>
                {
                    { "Default", new LocalStateProfileMetadata("Personal", "avatar1") },
                    { "Profile 1", new LocalStateProfileMetadata("Work", "avatar2") }
                },
                "Default"
            )
        );

        parser.ParseAsync(@"C:\Brave\User Data\Local State").Returns(localState);

        fileSystem.DirectoryExists(@"C:\Brave\User Data\Default").Returns(true);
        fileSystem.DirectoryExists(@"C:\Brave\User Data\Profile 1").Returns(false);

        var service = new ProfileDiscoveryService(installService, parser, fileSystem, logger);

        var profiles = await service.GetProfilesAsync();

        profiles.Should().HaveCount(1);
        profiles[0].FolderName.Should().Be("Default");
        profiles[0].DisplayName.Should().Be("Personal");
    }
}
