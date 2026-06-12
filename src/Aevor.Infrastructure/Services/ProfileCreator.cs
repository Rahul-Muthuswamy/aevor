using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class ProfileCreator : IProfileCreator
{
    private readonly IBraveInstallationService _installationService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProfileCreator> _logger;

    public ProfileCreator(
        IBraveInstallationService installationService,
        IFileSystem fileSystem,
        ILogger<ProfileCreator> logger)
    {
        _installationService = installationService;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<ProfileCreationResult> CreateProfileAsync(ProfileCreationRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _logger.LogInformation("Profile creation started for name: {ProfileName}", request.ProfileName);

        if (_installationService.IsBraveRunning())
        {
            _logger.LogWarning("Profile creation failed. Brave Browser is running.");
            return new ProfileCreationResult(false, null, "Brave Browser is running. Please close all Brave windows before proceeding.");
        }

        if (string.IsNullOrWhiteSpace(request.ProfileName))
        {
            return new ProfileCreationResult(false, null, "Profile name cannot be empty.");
        }

        var userDataPath = _installationService.GetUserDataPath();
        var localStatePath = Path.Combine(userDataPath, "Local State");

        if (!_fileSystem.FileExists(localStatePath))
        {
            return new ProfileCreationResult(false, null, $"Local State file not found at: {localStatePath}");
        }

        try
        {
            var localStateText = await _fileSystem.ReadAllTextAsync(localStatePath);
            var rootNode = JsonNode.Parse(localStateText);
            if (rootNode == null)
            {
                return new ProfileCreationResult(false, null, "Local State is corrupted/empty.");
            }

            var infoCacheNode = rootNode["profile"]?["info_cache"]?.AsObject();
            if (infoCacheNode == null)
            {
                return new ProfileCreationResult(false, null, "Local State is missing profile.info_cache structure.");
            }

            // Verify unique profile name (both display name and folder name)
            foreach (var property in infoCacheNode)
            {
                var name = property.Value?["name"]?.GetValue<string>();
                if (request.ProfileName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Profile creation failed. Profile name '{ProfileName}' conflicts with an existing profile.", request.ProfileName);
                    return new ProfileCreationResult(false, null, $"A profile with name '{request.ProfileName}' already exists.");
                }
            }

            // Determine folder name
            string folderName = request.FolderName;
            if (string.IsNullOrWhiteSpace(folderName))
            {
                int index = 1;
                while (infoCacheNode.ContainsKey($"Profile {index}"))
                {
                    index++;
                }
                folderName = $"Profile {index}";
            }

            if (infoCacheNode.ContainsKey(folderName))
            {
                return new ProfileCreationResult(false, null, $"Profile folder '{folderName}' already exists.");
            }

            var profilePath = Path.Combine(userDataPath, folderName);
            if (_fileSystem.DirectoryExists(profilePath))
            {
                return new ProfileCreationResult(false, null, $"Directory '{profilePath}' already exists on disk.");
            }

            // 1. Create profile directory
            _fileSystem.CreateDirectory(profilePath);

            // Create required profile files (Preferences and Secure Preferences)
            await _fileSystem.WriteAllTextAsync(Path.Combine(profilePath, "Preferences"), "{}");
            await _fileSystem.WriteAllTextAsync(Path.Combine(profilePath, "Secure Preferences"), "{}");

            // 2. Register profile inside Local State
            var newProfileMeta = new JsonObject
            {
                ["name"] = request.ProfileName,
                ["avatar_icon"] = request.AvatarIcon ?? "chrome://theme/IDR_PROFILE_AVATAR_0"
            };
            infoCacheNode.Add(folderName, newProfileMeta);

            // Write back to Local State
            var updatedLocalStateText = rootNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(localStatePath, updatedLocalStateText);

            _logger.LogInformation("Registration completed inside Local State for folder: {FolderName}", folderName);

            var registration = new ProfileRegistrationInfo(
                FolderName: folderName,
                DisplayName: request.ProfileName,
                ProfilePath: profilePath,
                RegisteredAt: DateTime.UtcNow
            );

            var braveProfile = new BraveProfile(
                FolderName: folderName,
                DisplayName: request.ProfileName,
                IsDefault: folderName.Equals("Default", StringComparison.OrdinalIgnoreCase),
                IsLastUsed: false,
                ProfilePath: profilePath
            );

            // 3. Validate registration
            var validation = await ValidateProfileAsync(folderName);
            if (!validation.IsValid)
            {
                var errors = string.Join("; ", validation.Errors);
                return new ProfileCreationResult(false, braveProfile, $"Profile registered but validation failed: {errors}", registration);
            }

            _logger.LogInformation("Profile creation completed successfully for folder: {FolderName}", folderName);
            return new ProfileCreationResult(true, braveProfile, null, registration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Profile creation failed due to exception.");
            throw new ProfileCreationException($"Failed to create profile: {ex.Message}", ex);
        }
    }

    public async Task<bool> DeleteProfileAsync(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return false;
        }

        _logger.LogInformation("Profile deletion started for folder: {FolderName}", folderName);

        var userDataPath = _installationService.GetUserDataPath();
        var localStatePath = Path.Combine(userDataPath, "Local State");

        try
        {
            // Remove from Local State
            if (_fileSystem.FileExists(localStatePath))
            {
                var localStateText = await _fileSystem.ReadAllTextAsync(localStatePath);
                var rootNode = JsonNode.Parse(localStateText);
                var infoCacheNode = rootNode?["profile"]?["info_cache"]?.AsObject();
                if (infoCacheNode != null && infoCacheNode.Remove(folderName))
                {
                    var updatedLocalStateText = rootNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await _fileSystem.WriteAllTextAsync(localStatePath, updatedLocalStateText);
                    _logger.LogInformation("Profile removed from Local State: {FolderName}", folderName);
                }
            }

            // Delete directory
            var profilePath = Path.Combine(userDataPath, folderName);
            if (_fileSystem.DirectoryExists(profilePath))
            {
                _fileSystem.DeleteDirectory(profilePath, true);
                _logger.LogInformation("Profile directory deleted: {ProfilePath}", profilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete profile: {FolderName}", folderName);
            return false;
        }
    }

    public async Task<ProfileValidationResult> ValidateProfileAsync(string folderName)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(folderName))
        {
            errors.Add("Folder name is null or empty.");
            return new ProfileValidationResult(false, errors, warnings);
        }

        var userDataPath = _installationService.GetUserDataPath();
        var profilePath = Path.Combine(userDataPath, folderName);

        // Verify directory exists
        if (!_fileSystem.DirectoryExists(profilePath))
        {
            errors.Add($"Profile directory does not exist on disk: {profilePath}");
        }

        // Verify required files present
        var preferencesPath = Path.Combine(profilePath, "Preferences");
        if (!_fileSystem.FileExists(preferencesPath))
        {
            errors.Add($"Required file 'Preferences' is missing at: {preferencesPath}");
        }

        // Verify registered in Local State
        var localStatePath = Path.Combine(userDataPath, "Local State");
        if (!_fileSystem.FileExists(localStatePath))
        {
            errors.Add("Local State file is missing.");
        }
        else
        {
            try
            {
                var localStateText = await _fileSystem.ReadAllTextAsync(localStatePath);
                var rootNode = JsonNode.Parse(localStateText);
                var infoCacheNode = rootNode?["profile"]?["info_cache"]?.AsObject();
                if (infoCacheNode == null || !infoCacheNode.ContainsKey(folderName))
                {
                    errors.Add($"Profile folder '{folderName}' is not registered in Local State.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to parse Local State during validation: {ex.Message}");
            }
        }

        return new ProfileValidationResult(errors.Count == 0, errors, warnings);
    }
}
