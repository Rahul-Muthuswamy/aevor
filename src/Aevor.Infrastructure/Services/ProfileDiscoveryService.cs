using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class ProfileDiscoveryService : IProfileDiscoveryService
{
    private readonly IBraveInstallationService _installationService;
    private readonly ILocalStateParser _localStateParser;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProfileDiscoveryService> _logger;

    public ProfileDiscoveryService(
        IBraveInstallationService installationService,
        ILocalStateParser localStateParser,
        IFileSystem fileSystem,
        ILogger<ProfileDiscoveryService> logger)
    {
        _installationService = installationService;
        _localStateParser = localStateParser;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<List<BraveProfile>> GetProfilesAsync()
    {
        _logger.LogInformation("Starting Brave profile discovery.");

        if (!_installationService.IsInstalled())
        {
            _logger.LogError("Brave Browser is not installed.");
            throw new BraveNotInstalledException("Brave Browser installation not detected on this system.");
        }

        var userDataPath = _installationService.GetUserDataPath();
        var localStatePath = Path.Combine(userDataPath, "Local State");

        _logger.LogInformation("Reading Local State file at: {LocalStatePath}", localStatePath);
        var localState = await _localStateParser.ParseAsync(localStatePath);

        var discoveredProfiles = new List<BraveProfile>();
        var lastUsed = localState.Profile.LastUsed;

        foreach (var (folderName, profileMeta) in localState.Profile.InfoCache)
        {
            var profilePath = Path.Combine(userDataPath, folderName);

            if (!_fileSystem.DirectoryExists(profilePath))
            {
                _logger.LogWarning("Profile folder defined in Local State does not exist on disk: {ProfilePath}", profilePath);
                continue;
            }

            var isDefault = folderName.Equals("Default", StringComparison.OrdinalIgnoreCase);
            var isLastUsed = folderName.Equals(lastUsed, StringComparison.OrdinalIgnoreCase);

            var profile = new BraveProfile(
                FolderName: folderName,
                DisplayName: profileMeta.Name,
                IsDefault: isDefault,
                IsLastUsed: isLastUsed,
                ProfilePath: profilePath
            );

            discoveredProfiles.Add(profile);
            _logger.LogInformation("Discovered profile: {DisplayName} ({FolderName})", profile.DisplayName, profile.FolderName);
        }

        _logger.LogInformation("Profile discovery completed. Found {Count} active profiles.", discoveredProfiles.Count);
        return discoveredProfiles;
    }
}
