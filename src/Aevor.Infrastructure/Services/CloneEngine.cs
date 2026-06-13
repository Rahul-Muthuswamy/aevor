using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class CloneEngine : ICloneEngine
{
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly IProfileAnalyzer _profileAnalyzer;
    private readonly ISecurityScanner _securityScanner;
    private readonly ITemplateBuilder _templateBuilder;
    private readonly IBackupService _backupService;
    private readonly IProfileCreator _profileCreator;
    private readonly ITemplateApplier _templateApplier;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CloneEngine> _logger;

    public CloneEngine(
        IProfileDiscoveryService profileDiscoveryService,
        IProfileAnalyzer profileAnalyzer,
        ISecurityScanner securityScanner,
        ITemplateBuilder templateBuilder,
        IBackupService backupService,
        IProfileCreator profileCreator,
        ITemplateApplier templateApplier,
        IFileSystem fileSystem,
        ILogger<CloneEngine> logger)
    {
        _profileDiscoveryService = profileDiscoveryService;
        _profileAnalyzer = profileAnalyzer;
        _securityScanner = securityScanner;
        _templateBuilder = templateBuilder;
        _backupService = backupService;
        _profileCreator = profileCreator;
        _templateApplier = templateApplier;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<CloneResult> CloneProfileAsync(CloneRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        _logger.LogInformation("Clone operation started. Source: {Source}, Dest Name: {DestName}", 
            request.SourceProfileFolderName, request.DestinationProfileName);

        try
        {
            // 1. Find and validate source profile
            var profiles = await _profileDiscoveryService.GetProfilesAsync();
            var sourceProfile = profiles.FirstOrDefault(p => p.FolderName.Equals(request.SourceProfileFolderName, StringComparison.OrdinalIgnoreCase));
            if (sourceProfile == null)
            {
                _logger.LogError("Clone failed. Source profile folder not found: {FolderName}", request.SourceProfileFolderName);
                return new CloneResult(false, $"Source profile '{request.SourceProfileFolderName}' not found.");
            }

            // 2. Run Profile Analyzer on source profile
            _logger.LogInformation("Running Profile Analyzer on source: {FolderName}", sourceProfile.FolderName);
            var analysisResult = await _profileAnalyzer.AnalyzeAsync(sourceProfile);

            // 3. Run Security Scanner on source profile
            _logger.LogInformation("Running Security Scanner on source: {FolderName}", sourceProfile.FolderName);
            var scanResult = await _securityScanner.ScanAsync(sourceProfile);

            // 4. Generate temporary template
            _logger.LogInformation("Generating temporary template for cloning.");
            var tempTemplate = _templateBuilder.Build(
                analysisResult, 
                scanResult, 
                request.DestinationProfileName, 
                $"Clone template from {sourceProfile.DisplayName}"
            );

            // Filter template based on user preferences in the CloneRequest
            if (!request.IncludeExtensions || !request.CopyExtensions)
            {
                tempTemplate = tempTemplate with { Extensions = Array.Empty<ExtensionInfo>() };
            }

            if (!request.IncludeThemes)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { Theme = null! } };
            }
            else if (!request.CopyThemes)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { Theme = new ThemeInformation(null, "System", null) } };
            }

            if (!request.IncludeSearchEngines)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { SearchEngine = null! } };
            }
            else if (!request.CopySearchEngines)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { SearchEngine = new SearchEngineInformation("Brave", "brave", "https://search.brave.com/search?q={searchTerms}") } };
            }

            if (!request.IncludeSettings)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { Sidebar = null!, VerticalTabs = null! } };
            }
            else if (!request.CopySettings)
            {
                tempTemplate = tempTemplate with { Settings = tempTemplate.Settings with { Sidebar = new SidebarConfiguration(false, "right"), VerticalTabs = new VerticalTabsConfiguration(false) } };
            }

            // TODO: Bookmarks are not implemented in TemplateBuilder or TemplateSettings yet, so bookmarks cannot be filtered here.

            // 5. Create backup of source profile if requested
            if (request.CreateBackup)
            {
                _logger.LogInformation("Creating backup of source profile before cloning.");
                var backupResult = await _backupService.CreateBackupAsync(sourceProfile);
                if (!backupResult.IsSuccess)
                {
                    _logger.LogError("Clone failed. Source backup creation failed: {Error}", backupResult.ErrorMessage);
                    return new CloneResult(false, $"Source backup failed: {backupResult.ErrorMessage}");
                }
            }

            // 6. Create destination profile
            _logger.LogInformation("Creating destination profile: {DestName}", request.DestinationProfileName);
            var creationReq = new ProfileCreationRequest(
                ProfileName: request.DestinationProfileName,
                FolderName: request.DestinationProfileFolderName,
                AvatarIcon: "chrome://theme/IDR_PROFILE_AVATAR_0"
            );
            var creationResult = await _profileCreator.CreateProfileAsync(creationReq);
            if (!creationResult.IsSuccess || creationResult.Profile == null)
            {
                _logger.LogError("Clone failed. Destination profile creation failed: {Error}", creationResult.ErrorMessage);
                return new CloneResult(false, $"Destination profile creation failed: {creationResult.ErrorMessage}");
            }
            var destProfile = creationResult.Profile;

            // Copy non-sensitive source profile files to destination profile folder
            _logger.LogInformation("Copying profile files from source {Source} to dest {Dest}", sourceProfile.ProfilePath, destProfile.ProfilePath);
            CopyProfileDirectory(sourceProfile.ProfilePath, destProfile.ProfilePath, request);

            // Update the profile name in the destination's Preferences file so Brave displays the cloned profile name correctly
            try
            {
                var destPrefPath = Path.Combine(destProfile.ProfilePath, "Preferences");
                if (_fileSystem.FileExists(destPrefPath))
                {
                    var prefText = await _fileSystem.ReadAllTextAsync(destPrefPath);
                    var prefRoot = System.Text.Json.Nodes.JsonNode.Parse(prefText);
                    if (prefRoot != null)
                    {
                        var profileNode = prefRoot["profile"];
                        if (profileNode == null)
                        {
                            prefRoot.AsObject()["profile"] = new System.Text.Json.Nodes.JsonObject();
                            profileNode = prefRoot["profile"];
                        }
                        profileNode!.AsObject()["name"] = System.Text.Json.Nodes.JsonValue.Create(request.DestinationProfileName);
                        await _fileSystem.WriteAllTextAsync(destPrefPath, prefRoot.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                        _logger.LogInformation("Updated profile name to '{DestName}' in Preferences.", request.DestinationProfileName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update profile name in cloned Preferences file.");
            }

            // 7. Apply template to destination profile (skip backup since it is a brand new cloned profile)
            _logger.LogInformation("Applying template settings to destination profile: {FolderName}", destProfile.FolderName);
            var applyResult = await _templateApplier.ApplyTemplateAsync(tempTemplate, destProfile, skipBackup: true);
            if (!applyResult.IsSuccess)
            {
                _logger.LogError("Clone failed. Failed to apply template: {Error}", applyResult.ErrorMessage);
                // Clean up destination
                await _profileCreator.DeleteProfileAsync(destProfile.FolderName);
                return new CloneResult(false, $"Failed to apply settings to clone: {applyResult.ErrorMessage}");
            }

            // 8. Validate cloned profile
            _logger.LogInformation("Validating cloned profile: {FolderName}", destProfile.FolderName);
            var validationResult = await _profileCreator.ValidateProfileAsync(destProfile.FolderName);

            // 9. Generate clone report
            var settingsCopied = new List<string>();
            if (tempTemplate.Settings?.Theme != null) settingsCopied.Add("Theme");
            if (tempTemplate.Settings?.SearchEngine != null) settingsCopied.Add("Search Engine");
            if (tempTemplate.Settings?.Sidebar != null) settingsCopied.Add("Sidebar");
            if (tempTemplate.Settings?.VerticalTabs != null) settingsCopied.Add("Vertical Tabs");

            var extensionsCopied = tempTemplate.Extensions.Select(e => $"{e.Name} ({e.Id})").ToList();
            var warnings = tempTemplate.Warnings.Select(w => w.Message).ToList();

            var report = new CloneReport(
                SourceProfile: sourceProfile,
                DestinationProfile: destProfile,
                SettingsCopied: settingsCopied,
                ExtensionsCopied: extensionsCopied,
                ExcludedArtifacts: tempTemplate.ExcludedArtifacts,
                Warnings: warnings,
                ValidationResult: validationResult
            );

            _logger.LogInformation("Clone operation completed successfully. Dest: {FolderName}", destProfile.FolderName);
            return new CloneResult(true, null, report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Clone workflow failed due to exception.");
            throw new CloneException($"Clone workflow failed: {ex.Message}", ex);
        }
    }

    public async Task<CloneValidationResult> ValidateCloneAsync(string sourceFolderName, string destFolderName)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // 1. Verify source and destination profiles exist
        var profiles = await _profileDiscoveryService.GetProfilesAsync();
        var sourceProfile = profiles.FirstOrDefault(p => p.FolderName.Equals(sourceFolderName, StringComparison.OrdinalIgnoreCase));
        var destProfile = profiles.FirstOrDefault(p => p.FolderName.Equals(destFolderName, StringComparison.OrdinalIgnoreCase));

        if (sourceProfile == null)
        {
            errors.Add($"Source profile folder '{sourceFolderName}' does not exist.");
            return new CloneValidationResult(false, errors, warnings);
        }

        if (destProfile == null)
        {
            errors.Add($"Destination profile folder '{destFolderName}' does not exist.");
            return new CloneValidationResult(false, errors, warnings);
        }

        try
        {
            // 2. Run Profile Analyzer on both
            var sourceAnalysis = await _profileAnalyzer.AnalyzeAsync(sourceProfile);
            var destAnalysis = await _profileAnalyzer.AnalyzeAsync(destProfile);

            // 3. Verify settings copied correctly
            if (sourceAnalysis.Sidebar.ShowSidebar != destAnalysis.Sidebar.ShowSidebar ||
                sourceAnalysis.Sidebar.Position != destAnalysis.Sidebar.Position)
            {
                errors.Add("Sidebar configuration mismatch between source and clone.");
            }

            if (sourceAnalysis.VerticalTabs.UseVerticalTabs != destAnalysis.VerticalTabs.UseVerticalTabs)
            {
                errors.Add("Vertical tabs configuration mismatch between source and clone.");
            }

            // 4. Verify extensions copied correctly
            foreach (var srcExt in sourceAnalysis.InstalledExtensions)
            {
                var destExt = destAnalysis.InstalledExtensions.FirstOrDefault(e => e.Id == srcExt.Id);
                if (destExt == null)
                {
                    errors.Add($"Extension '{srcExt.Name}' ({srcExt.Id}) missing in cloned profile.");
                }
                else if (destExt.IsEnabled != srcExt.IsEnabled)
                {
                    errors.Add($"Extension '{srcExt.Name}' ({srcExt.Id}) enabled state mismatch in cloned profile.");
                }
            }

            // 5. Verify security exclusions respected
            var destScan = await _securityScanner.ScanAsync(destProfile);
            if (destScan.HasPasswords)
            {
                errors.Add("Security violation: Cloned profile contains password database.");
            }
            if (destScan.HasCookies)
            {
                errors.Add("Security violation: Cloned profile contains active session cookies database.");
            }
            if (destScan.HasWalletData)
            {
                errors.Add("Security violation: Cloned profile contains cryptocurrency wallet data.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Clone validation failed due to analyzer/scanner error: {ex.Message}");
        }

        return new CloneValidationResult(errors.Count == 0, errors, warnings);
    }

    public async Task<ClonePreview> PreviewCloneAsync(CloneRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var settingsToCopy = new List<string>();
        var extensionsToCopy = new List<string>();
        var warnings = new List<string>();

        var profiles = await _profileDiscoveryService.GetProfilesAsync();
        var sourceProfile = profiles.FirstOrDefault(p => p.FolderName.Equals(request.SourceProfileFolderName, StringComparison.OrdinalIgnoreCase));
        if (sourceProfile == null)
        {
            warnings.Add($"Source profile '{request.SourceProfileFolderName}' does not exist.");
            return new ClonePreview(request.SourceProfileFolderName, request.DestinationProfileName, settingsToCopy, extensionsToCopy, warnings);
        }

        try
        {
            var analysis = await _profileAnalyzer.AnalyzeAsync(sourceProfile);
            
            if (request.CopyThemes && request.IncludeThemes)
            {
                settingsToCopy.Add("Theme Information");
            }
            if (request.CopySearchEngines && request.IncludeSearchEngines)
            {
                settingsToCopy.Add("Search Engine Settings");
            }
            if (request.CopySettings && request.IncludeSettings)
            {
                settingsToCopy.Add($"Sidebar Layout (Show: {analysis.Sidebar.ShowSidebar}, Position: {analysis.Sidebar.Position ?? "Default"})");
                settingsToCopy.Add($"Vertical Tabs (Enabled: {analysis.VerticalTabs.UseVerticalTabs})");
            }

            if (request.CopyExtensions && request.IncludeExtensions)
            {
                foreach (var ext in analysis.InstalledExtensions)
                {
                    extensionsToCopy.Add($"{ext.Name} ({ext.Id})");
                }
            }

            var scan = await _securityScanner.ScanAsync(sourceProfile);
            if (scan.HasPasswords || scan.HasCookies || scan.HasWalletData)
            {
                warnings.Add("Sensitive credentials or session data detected in source profile. These will be automatically sanitized and excluded from the clone.");
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not complete preview analysis: {ex.Message}");
        }

        return new ClonePreview(request.SourceProfileFolderName, request.DestinationProfileName, settingsToCopy, extensionsToCopy, warnings);
    }

    private void CopyProfileDirectory(string sourceDir, string destDir, CloneRequest request)
    {
        var files = _fileSystem.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories).ToList();
        foreach (var file in files)
        {
            if (ShouldExcludeFromClone(file, request))
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destPath = Path.Combine(destDir, relativePath);

            var destFileDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(destFileDir) && !_fileSystem.DirectoryExists(destFileDir))
            {
                _fileSystem.CreateDirectory(destFileDir);
            }

            try
            {
                _fileSystem.CopyFile(file, destPath, true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy file {File} during cloning. Skipping.", file);
            }
        }
    }

    private bool ShouldExcludeFromClone(string filePath, CloneRequest request)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(fileName)) return true;

        // Exclude lock and socket files
        if (fileName.Equals("lockfile", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("parent.lock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonLock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonCookie", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("SingletonSocket", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
            fileName.Contains("socket", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Exclude extensions if CopyExtensions or IncludeExtensions is false
        if (!request.CopyExtensions || !request.IncludeExtensions)
        {
            var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (parts.Any(p => p.Equals("Extensions", StringComparison.OrdinalIgnoreCase) ||
                               p.Equals("Extension Rules", StringComparison.OrdinalIgnoreCase) ||
                               p.Equals("Extension State", StringComparison.OrdinalIgnoreCase) ||
                               p.Equals("Local Extension Settings", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Exclude bookmarks if CopyBookmarks or IncludeBookmarks is false
        if (!request.CopyBookmarks || !request.IncludeBookmarks)
        {
            if (fileName.Equals("Bookmarks", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Bookmarks.bak", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Exclude preferences files if CopySettings/IncludeSettings, CopyThemes/IncludeThemes, and CopySearchEngines/IncludeSearchEngines are all false
        bool copySettings = request.CopySettings && request.IncludeSettings;
        bool copyThemes = request.CopyThemes && request.IncludeThemes;
        bool copySearchEngines = request.CopySearchEngines && request.IncludeSearchEngines;
        if (!copySettings && !copyThemes && !copySearchEngines)
        {
            if (fileName.Equals("Preferences", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Secure Preferences", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Exclude directories/files that contain sensitive data
        var pathParts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (pathParts.Any(p => 
            p.Equals("Cache", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Code Cache", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("GPUCache", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("BraveWallet", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Sessions", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Session Storage", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("IndexedDB", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Local Storage", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Local Extension Settings", StringComparison.OrdinalIgnoreCase) ||
            p.Equals("Extension State", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Exclude specific sensitive files
        if (fileName.Equals("Login Data", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Login Data For Account", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Cookies", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Cookies-journal", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Web Data", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("Web Data-journal", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("History", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("History-journal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
