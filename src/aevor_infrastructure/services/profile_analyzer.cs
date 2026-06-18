using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class ProfileAnalyzer : IProfileAnalyzer
{
    private readonly IPreferencesParser _preferencesParser;
    private readonly ISecurePreferencesParser _securePreferencesParser;
    private readonly IDiscoveredSettingRegistry _registry;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ProfileAnalyzer> _logger;

    public ProfileAnalyzer(
        IPreferencesParser preferencesParser,
        ISecurePreferencesParser securePreferencesParser,
        IDiscoveredSettingRegistry registry,
        IFileSystem fileSystem,
        ILogger<ProfileAnalyzer> logger)
    {
        _preferencesParser = preferencesParser;
        _securePreferencesParser = securePreferencesParser;
        _registry = registry;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<ProfileAnalysisResult> AnalyzeAsync(BraveProfile profile)
    {
        _logger.LogInformation("Starting profile analysis for {ProfileName} at path {ProfilePath}.", profile.DisplayName, profile.ProfilePath);

        if (!_fileSystem.DirectoryExists(profile.ProfilePath))
        {
            _logger.LogError("Profile directory does not exist: {ProfilePath}", profile.ProfilePath);
            throw new CorruptedProfileException($"Profile directory does not exist: {profile.ProfilePath}");
        }

        var prefPath = Path.Combine(profile.ProfilePath, "Preferences");
        var secPrefPath = Path.Combine(profile.ProfilePath, "Secure Preferences");

        BrowserSettings prefSettings;
        try
        {
            prefSettings = await _preferencesParser.ParseAsync(prefPath);
        }
        catch (PreferencesFileNotFoundException ex)
        {
            _logger.LogError(ex, "Preferences file missing at {PrefPath}.", prefPath);
            throw;
        }
        catch (InvalidPreferencesJsonException ex)
        {
            _logger.LogError(ex, "Invalid Preferences JSON at {PrefPath}.", prefPath);
            throw;
        }
        catch (ProfileAccessDeniedException ex)
        {
            _logger.LogError(ex, "Access denied to Preferences file at {PrefPath}.", prefPath);
            throw;
        }

        BrowserSettings secPrefSettings;
        try
        {
            secPrefSettings = await _securePreferencesParser.ParseAsync(secPrefPath);
        }
        catch (SecurePreferencesFileNotFoundException ex)
        {
            _logger.LogError(ex, "Secure Preferences file missing at {SecPrefPath}.", secPrefPath);
            throw;
        }
        catch (InvalidSecurePreferencesJsonException ex)
        {
            _logger.LogError(ex, "Invalid Secure Preferences JSON at {SecPrefPath}.", secPrefPath);
            throw;
        }
        catch (ProfileAccessDeniedException ex)
        {
            _logger.LogError(ex, "Access denied to Secure Preferences file at {SecPrefPath}.", secPrefPath);
            throw;
        }

        var warnings = new List<string>();
        var errors = new List<string>();

        foreach (var prefExt in prefSettings.Extensions)
        {
            var secExt = secPrefSettings.Extensions.FirstOrDefault(e => e.Id == prefExt.Id);
            if (secExt == null)
            {
                var warnMsg = $"Extension '{prefExt.Name}' ({prefExt.Id}) is present in Preferences but missing from Secure Preferences.";
                warnings.Add(warnMsg);
                _logger.LogWarning(warnMsg);
            }
            else if (prefExt.IsEnabled != secExt.IsEnabled)
            {
                var warnMsg = $"Extension '{prefExt.Name}' ({prefExt.Id}) state mismatch. Preferences enabled: {prefExt.IsEnabled}, Secure Preferences enabled: {secExt.IsEnabled}.";
                warnings.Add(warnMsg);
                _logger.LogWarning(warnMsg);
            }
        }

        await _registry.SaveAsync();

        _logger.LogInformation("Profile analysis completed successfully for {ProfileName}.", profile.DisplayName);

        return new ProfileAnalysisResult(
            ProfileName: profile.DisplayName,
            ProfilePath: profile.ProfilePath,
            Theme: prefSettings.Theme,
            SearchEngine: prefSettings.SearchEngine,
            Sidebar: prefSettings.Sidebar,
            VerticalTabs: prefSettings.VerticalTabs,
            InstalledExtensions: prefSettings.Extensions,
            ExtensionCount: prefSettings.Extensions.Count,
            AnalysisTimestamp: DateTime.UtcNow,
            Warnings: warnings,
            Errors: errors
        );
    }
}
