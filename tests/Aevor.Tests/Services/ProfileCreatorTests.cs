using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

public class ProfileCreatorTests
{
    private readonly IBraveInstallationService _installationService = Substitute.For<IBraveInstallationService>();
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly NullLogger<ProfileCreator> _logger = NullLogger<ProfileCreator>.Instance;
    private readonly ProfileCreator _creator;

    public ProfileCreatorTests()
    {
        _installationService.GetUserDataPath().Returns("C:\\Brave\\UserData");
        _creator = new ProfileCreator(_installationService, _fileSystem, _logger);
    }

    [Fact]
    public async Task CreateProfileAsync_ValidRequest_ShouldCreateProfileSuccessfully()
    {
        // Arrange
        var request = new ProfileCreationRequest("New Profile");
        var localStatePath = "C:\\Brave\\UserData\\Local State";

        var initialLocalState = @"{
            ""profile"": {
                ""info_cache"": {
                    ""Default"": {
                        ""name"": ""Personal Profile""
                    }
                }
            }
        }";

        var localStateContent = initialLocalState;
        var createdDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var createdFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        _fileSystem.FileExists(Arg.Any<string>()).Returns(callInfo =>
        {
            var path = (string)callInfo[0];
            if (path.Equals(localStatePath, StringComparison.OrdinalIgnoreCase)) return true;
            return createdFiles.Contains(path);
        });

        _fileSystem.ReadAllTextAsync(localStatePath).Returns(_ => Task.FromResult(localStateContent));

        _fileSystem.DirectoryExists(Arg.Any<string>()).Returns(callInfo =>
        {
            var path = (string)callInfo[0];
            return createdDirectories.Contains(path);
        });

        _fileSystem.When(x => x.CreateDirectory(Arg.Any<string>()))
            .Do(callInfo => createdDirectories.Add((string)callInfo[0]));

        _fileSystem.WriteAllTextAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(callInfo =>
        {
            var path = (string)callInfo[0];
            var contents = (string)callInfo[1];
            if (path.Equals(localStatePath, StringComparison.OrdinalIgnoreCase))
            {
                localStateContent = contents;
            }
            else
            {
                createdFiles.Add(path);
            }
            return Task.CompletedTask;
        });

        // Act
        var result = await _creator.CreateProfileAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Profile.Should().NotBeNull();
        result.Profile!.DisplayName.Should().Be("New Profile");
        result.Profile.FolderName.Should().Be("Profile 1");
        result.Profile.ProfilePath.Should().Be("C:\\Brave\\UserData\\Profile 1");

        _fileSystem.Received(1).CreateDirectory("C:\\Brave\\UserData\\Profile 1");
        await _fileSystem.Received(1).WriteAllTextAsync("C:\\Brave\\UserData\\Profile 1\\Preferences", "{}");
        await _fileSystem.Received(1).WriteAllTextAsync("C:\\Brave\\UserData\\Profile 1\\Secure Preferences", "{}");
        await _fileSystem.Received(1).WriteAllTextAsync(localStatePath, Arg.Is<string>(s => s.Contains("New Profile")));
    }

    [Fact]
    public async Task CreateProfileAsync_DuplicateName_ShouldReturnFailure()
    {
        // Arrange
        var request = new ProfileCreationRequest("Personal Profile");
        var localStatePath = "C:\\Brave\\UserData\\Local State";

        var initialLocalState = @"{
            ""profile"": {
                ""info_cache"": {
                    ""Default"": {
                        ""name"": ""Personal Profile""
                    }
                }
            }
        }";

        _fileSystem.FileExists(localStatePath).Returns(true);
        _fileSystem.ReadAllTextAsync(localStatePath).Returns(initialLocalState);

        // Act
        var result = await _creator.CreateProfileAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("already exists");
        _fileSystem.DidNotReceive().CreateDirectory(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteProfileAsync_ExistingProfile_ShouldReturnTrueAndCleanup()
    {
        // Arrange
        var folderName = "Profile 1";
        var localStatePath = "C:\\Brave\\UserData\\Local State";
        var localState = @"{
            ""profile"": {
                ""info_cache"": {
                    ""Profile 1"": {
                        ""name"": ""Profile One""
                    }
                }
            }
        }";

        _fileSystem.FileExists(localStatePath).Returns(true);
        _fileSystem.ReadAllTextAsync(localStatePath).Returns(localState);
        _fileSystem.DirectoryExists("C:\\Brave\\UserData\\Profile 1").Returns(true);

        // Act
        var result = await _creator.DeleteProfileAsync(folderName);

        // Assert
        result.Should().BeTrue();
        _fileSystem.Received(1).DeleteDirectory("C:\\Brave\\UserData\\Profile 1", true);
        await _fileSystem.Received(1).WriteAllTextAsync(localStatePath, Arg.Is<string>(s => !s.Contains("Profile 1")));
    }

    [Fact]
    public async Task ValidateProfileAsync_MissingDirectory_ShouldReturnInvalid()
    {
        // Arrange
        var folderName = "Profile 1";
        _fileSystem.DirectoryExists("C:\\Brave\\UserData\\Profile 1").Returns(false);

        // Act
        var result = await _creator.ValidateProfileAsync(folderName);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("does not exist"));
    }
}
