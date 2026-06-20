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

public class TemplateApplier : ITemplateApplier
{
    private readonly IFileSystem _fileSystem;
    private readonly ITemplateValidator _templateValidator;
    private readonly IBackupService _backupService;
    private readonly IProfileDiscoveryService _profileDiscoveryService;
    private readonly IBraveInstallationService _braveInstallationService;
    private readonly ILogger<TemplateApplier> _logger;

    public TemplateApplier(
        IFileSystem fileSystem,
        ITemplateValidator templateValidator,
        IBackupService backupService,
        IProfileDiscoveryService profileDiscoveryService,
        IBraveInstallationService braveInstallationService,
        ILogger<TemplateApplier> logger)
    {
        _fileSystem = fileSystem;
        _templateValidator = templateValidator;
        _backupService = backupService;
        _profileDiscoveryService = profileDiscoveryService;
        _braveInstallationService = braveInstallationService;
        _logger = logger;
    }

    public async Task<TemplateApplicationResult> ApplyTemplateAsync(AevorTemplate template, BraveProfile profile, bool skipBackup = false)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        _logger.LogInformation("Template application started for profile: {ProfileName}", profile.DisplayName);

        if (_braveInstallationService.IsBraveRunning())
        {
            _logger.LogError("Template application failed. Brave is currently running.");
            return new TemplateApplicationResult(false, "Brave is currently running. Please close Brave before applying templates.");
        }

        var templateVal = _templateValidator.Validate(template);
        if (!templateVal.IsValid)
        {
            var errorsStr = string.Join("; ", templateVal.Errors.Select(e => e.Message));
            _logger.LogError("Template application failed. Invalid template: {Errors}", errorsStr);
            return new TemplateApplicationResult(false, $"Invalid template: {errorsStr}");
        }

        if (!_fileSystem.DirectoryExists(profile.ProfilePath))
        {
            _logger.LogError("Template application failed. Target profile folder does not exist: {ProfilePath}", profile.ProfilePath);
            return new TemplateApplicationResult(false, $"Profile directory does not exist: {profile.ProfilePath}");
        }

        var prefPath = Path.Combine(profile.ProfilePath, "Preferences");
        var secPrefPath = Path.Combine(profile.ProfilePath, "Secure Preferences");

        if (!_fileSystem.FileExists(prefPath) || !_fileSystem.FileExists(secPrefPath))
        {
            _logger.LogError("Template application failed. Target profile preferences files are missing.");
            return new TemplateApplicationResult(false, "Profile preferences or secure preferences files are missing.");
        }

        Guid? backupId = null;
        if (!skipBackup)
        {
            _logger.LogInformation("Creating pre-modification backup of profile: {ProfileName}", profile.DisplayName);
            var backupResult = await _backupService.CreateBackupAsync(profile);
            if (!backupResult.IsSuccess || backupResult.Metadata == null)
            {
                _logger.LogError("Template application failed. Pre-modification backup failed: {Error}", backupResult.ErrorMessage);
                return new TemplateApplicationResult(false, $"Pre-modification backup failed: {backupResult.ErrorMessage}");
            }

            backupId = backupResult.Metadata.BackupId;
        }

        var appliedChanges = new List<string>();

        try
        {
            var prefText = await _fileSystem.ReadAllTextAsync(prefPath);
            var prefRoot = JsonNode.Parse(prefText) as JsonObject ?? new JsonObject();

            var secPrefText = await _fileSystem.ReadAllTextAsync(secPrefPath);
            var secPrefRoot = JsonNode.Parse(secPrefText) as JsonObject ?? new JsonObject();

            var idsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (template.Extensions != null)
            {
                foreach (var ext in template.Extensions)
                {
                    if (!string.IsNullOrEmpty(ext.Id))
                    {
                        idsToKeep.Add(ext.Id);
                    }
                }
            }
            if (template.Settings?.Theme?.ThemeId != null &&
                template.Settings.Theme.ThemeId.Length == 32 &&
                template.Settings.Theme.ThemeId.All(c => c >= 'a' && c <= 'p'))
            {
                idsToKeep.Add(template.Settings.Theme.ThemeId);
            }

            ClearExistingExtensions(prefRoot, secPrefRoot, profile.ProfilePath, idsToKeep);

            await CopyTemplateExtensionFilesAsync(template, profile, idsToKeep, secPrefRoot);

            if (template.Settings != null)
            {
                if (template.Settings.Theme != null)
                {
                    ApplyTheme(prefRoot, template.Settings.Theme);
                    appliedChanges.Add("Theme configurations applied.");
                }

                if (template.Settings.SearchEngine != null)
                {
                    ApplySearchEngine(prefRoot, template.Settings.SearchEngine);
                    appliedChanges.Add("Search Engine default configurations applied.");
                }

                if (template.Settings.Sidebar != null)
                {
                    ApplySidebar(prefRoot, template.Settings.Sidebar);
                    appliedChanges.Add("Sidebar configuration applied.");
                }

                if (template.Settings.VerticalTabs != null)
                {
                    ApplyVerticalTabs(prefRoot, template.Settings.VerticalTabs);
                    appliedChanges.Add("Vertical tabs configurations applied.");
                }
            }

            if (template.Extensions != null && template.Extensions.Count > 0)
            {
                ApplyExtensions(prefRoot, template.Extensions);
                ApplyExtensions(secPrefRoot, template.Extensions);
                appliedChanges.Add($"{template.Extensions.Count} Extensions configuration applied.");
            }

            var serializeOptions = new JsonSerializerOptions { WriteIndented = true };
            await _fileSystem.WriteAllTextAsync(prefPath, prefRoot.ToJsonString(serializeOptions));
            await _fileSystem.WriteAllTextAsync(secPrefPath, secPrefRoot.ToJsonString(serializeOptions));

            _logger.LogInformation("Template application completed successfully for profile: {ProfileName}. Backup ID: {BackupId}",
                profile.DisplayName, backupId);

            return new TemplateApplicationResult(true, null, backupId, appliedChanges);
        }
        catch (Exception ex)
        {
            if (backupId.HasValue)
            {
                _logger.LogError(ex, "Template application encountered an error. Triggering automatic rollback to backup: {BackupId}", backupId.Value);

                try
                {
                    var restoreResult = await _backupService.RestoreBackupAsync(backupId.Value);
                    if (restoreResult.IsSuccess)
                    {
                        _logger.LogInformation("Rollback completed successfully. Target profile reverted.");
                    }
                    else
                    {
                        _logger.LogCritical("Rollback failed! Profile may be in corrupted state. Error: {Error}", restoreResult.ErrorMessage);
                    }
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogCritical(rollbackEx, "Rollback failed with exception.");
                }
            }
            else
            {
                _logger.LogError(ex, "Template application encountered an error. No backup was created to roll back to.");
            }

            throw new TemplateApplicationException($"Template application failed. Rollback executed: {ex.Message}", ex);
        }
    }

    public async Task<TemplateApplicationValidationResult> ValidateApplicationAsync(AevorTemplate template, BraveProfile profile)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (template == null)
        {
            errors.Add("Template is null.");
            return new TemplateApplicationValidationResult(false, errors, warnings);
        }

        if (profile == null)
        {
            errors.Add("Profile is null.");
            return new TemplateApplicationValidationResult(false, errors, warnings);
        }

        if (template.Metadata?.TemplateVersion == null || template.Metadata.TemplateVersion.ToString() != "1.0")
        {
            errors.Add($"Unsupported or missing template version: '{template.Metadata?.TemplateVersion?.ToString() ?? "null"}'");
        }

        if (!_fileSystem.DirectoryExists(profile.ProfilePath))
        {
            errors.Add($"Profile directory does not exist: {profile.ProfilePath}");
        }

        return new TemplateApplicationValidationResult(errors.Count == 0, errors, warnings);
    }

    public async Task<TemplateApplicationPreview> PreviewChangesAsync(AevorTemplate template, BraveProfile profile)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        var settingsToModify = new List<string>();
        var extensionsToModify = new List<string>();
        var filesAffected = new List<string> { "Preferences", "Secure Preferences" };
        var warnings = new List<string>();

        if (template.Settings != null)
        {
            if (template.Settings.Theme != null) settingsToModify.Add("Theme setting");
            if (template.Settings.SearchEngine != null) settingsToModify.Add("Default Search Engine");
            if (template.Settings.Sidebar != null) settingsToModify.Add("Sidebar layout");
            if (template.Settings.VerticalTabs != null) settingsToModify.Add("Vertical tabs configuration");
        }

        if (template.Extensions != null)
        {
            foreach (var ext in template.Extensions)
            {
                extensionsToModify.Add($"{ext.Name} ({ext.Id}) State: {(ext.IsEnabled ? "Enabled" : "Disabled")}");
            }
        }

        if (template.Metadata?.SourceBrowserVersion != null)
        {
            _logger.LogInformation("Template was generated from browser version: {Version}", template.Metadata.SourceBrowserVersion);
        }

        return new TemplateApplicationPreview(settingsToModify, extensionsToModify, filesAffected, warnings);
    }

    private void ClearExistingExtensions(JsonNode prefRoot, JsonNode secPrefRoot, string profilePath, HashSet<string> idsToKeep)
    {
        _logger.LogInformation("Clearing target profile extensions not in template...");

        var prefExtensionsSettings = prefRoot["extensions"]?["settings"] as JsonObject;
        if (prefExtensionsSettings != null)
        {
            var toRemove = prefExtensionsSettings.Select(pair => pair.Key).Where(key => !idsToKeep.Contains(key)).ToList();
            foreach (var key in toRemove)
            {
                prefExtensionsSettings.Remove(key);
                _logger.LogDebug("Removed extension {ExtId} from Preferences", key);
            }
        }

        var secPrefExtensionsSettings = secPrefRoot["extensions"]?["settings"] as JsonObject;
        if (secPrefExtensionsSettings != null)
        {
            var toRemove = secPrefExtensionsSettings.Select(pair => pair.Key).Where(key => !idsToKeep.Contains(key)).ToList();
            foreach (var key in toRemove)
            {
                secPrefExtensionsSettings.Remove(key);
                _logger.LogDebug("Removed extension {ExtId} from Secure Preferences settings", key);
            }
        }

        var macsExtensionsSettings = secPrefRoot["protection"]?["macs"]?["extensions"]?["settings"] as JsonObject;
        if (macsExtensionsSettings != null)
        {
            var toRemove = macsExtensionsSettings.Select(pair => pair.Key).Where(key => !idsToKeep.Contains(key)).ToList();
            foreach (var key in toRemove)
            {
                macsExtensionsSettings.Remove(key);
                _logger.LogDebug("Removed extension signature {ExtId} from Secure Preferences", key);
            }
        }

        var macsExtensionsEncryptedHash = secPrefRoot["protection"]?["macs"]?["extensions"]?["settings_encrypted_hash"] as JsonObject;
        if (macsExtensionsEncryptedHash != null)
        {
            var toRemove = macsExtensionsEncryptedHash.Select(pair => pair.Key).Where(key => !idsToKeep.Contains(key)).ToList();
            foreach (var key in toRemove)
            {
                macsExtensionsEncryptedHash.Remove(key);
                _logger.LogDebug("Removed extension encrypted hash signature {ExtId} from Secure Preferences", key);
            }
        }

        var extensionsDir = Path.Combine(profilePath, "Extensions");
        if (_fileSystem.DirectoryExists(extensionsDir))
        {
            var existingDirs = _fileSystem.EnumerateDirectories(extensionsDir).ToList();
            foreach (var dir in existingDirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!idsToKeep.Contains(dirName) && dirName.Length == 32 && dirName.All(c => c >= 'a' && c <= 'p'))
                {
                    try
                    {
                        _fileSystem.DeleteDirectory(dir, true);
                        _logger.LogInformation("Deleted non-template extension directory: {Path}", dir);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete extension directory: {Path}", dir);
                    }
                }
            }
        }
    }

    private async Task CopyTemplateExtensionFilesAsync(AevorTemplate template, BraveProfile targetProfile, HashSet<string> idsToCopy, JsonNode targetSecPrefRoot)
    {
        var sourceProfileName = template.Metadata?.SourceProfileName;
        if (string.IsNullOrEmpty(sourceProfileName))
        {
            _logger.LogWarning("No source profile specified in template metadata. Skipping file/signature copy.");
            return;
        }

        _logger.LogInformation("Finding source profile for template: {SourceProfileName}", sourceProfileName);
        var profiles = await _profileDiscoveryService.GetProfilesAsync();
        var sourceProfile = profiles.FirstOrDefault(p =>
            string.Equals(p.DisplayName, sourceProfileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.FolderName, sourceProfileName, StringComparison.OrdinalIgnoreCase));

        if (sourceProfile == null)
        {
            _logger.LogWarning("Source profile '{SourceProfileName}' not found. Preferences will be applied, but extension files and signatures cannot be copied.", sourceProfileName);
            return;
        }

        if (!_fileSystem.DirectoryExists(sourceProfile.ProfilePath))
        {
            _logger.LogWarning("Source profile path '{SourceProfilePath}' does not exist. Skipping file/signature copy.", sourceProfile.ProfilePath);
            return;
        }

        _logger.LogInformation("Source profile found at: {SourceProfilePath}. Copying files and signatures...", sourceProfile.ProfilePath);

        var srcSecPrefPath = Path.Combine(sourceProfile.ProfilePath, "Secure Preferences");
        JsonObject? srcSecPrefRoot = null;
        if (_fileSystem.FileExists(srcSecPrefPath))
        {
            try
            {
                var srcSecPrefText = await _fileSystem.ReadAllTextAsync(srcSecPrefPath);
                srcSecPrefRoot = JsonNode.Parse(srcSecPrefText) as JsonObject;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse source Secure Preferences at '{Path}'", srcSecPrefPath);
            }
        }

        var targetSecPrefObj = targetSecPrefRoot as JsonObject;
        if (targetSecPrefObj != null)
        {
            var targetProtection = targetSecPrefObj["protection"] as JsonObject;
            if (targetProtection == null)
            {
                targetProtection = new JsonObject();
                targetSecPrefObj["protection"] = targetProtection;
            }
            var targetMacs = targetProtection["macs"] as JsonObject;
            if (targetMacs == null)
            {
                targetMacs = new JsonObject();
                targetProtection["macs"] = targetMacs;
            }
            var targetExtensions = targetMacs["extensions"] as JsonObject;
            if (targetExtensions == null)
            {
                targetExtensions = new JsonObject();
                targetMacs["extensions"] = targetExtensions;
            }
            var targetSettings = targetExtensions["settings"] as JsonObject;
            if (targetSettings == null)
            {
                targetSettings = new JsonObject();
                targetExtensions["settings"] = targetSettings;
            }
            var targetSettingsEncryptedHash = targetExtensions["settings_encrypted_hash"] as JsonObject;
            if (targetSettingsEncryptedHash == null)
            {
                targetSettingsEncryptedHash = new JsonObject();
                targetExtensions["settings_encrypted_hash"] = targetSettingsEncryptedHash;
            }

            var srcProtection = srcSecPrefRoot?["protection"] as JsonObject;
            var srcMacs = srcProtection?["macs"] as JsonObject;
            if (srcMacs != null)
            {
                if (srcMacs["default_search_provider"] != null)
                {
                    targetMacs["default_search_provider"] = srcMacs["default_search_provider"]?.DeepClone();
                }
                if (srcMacs["default_search_provider_data"] != null)
                {
                    targetMacs["default_search_provider_data"] = srcMacs["default_search_provider_data"]?.DeepClone();
                }
            }

            foreach (var extId in idsToCopy)
            {
                var srcDir = Path.Combine(sourceProfile.ProfilePath, "Extensions", extId);
                var destDir = Path.Combine(targetProfile.ProfilePath, "Extensions", extId);
                if (_fileSystem.DirectoryExists(srcDir))
                {
                    try
                    {
                        CopyDirectoryRecursive(srcDir, destDir);
                        _logger.LogInformation("Copied files for extension/theme: {ExtId}", extId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to copy directory from {Source} to {Dest}", srcDir, destDir);
                    }
                }
                else
                {
                    _logger.LogWarning("Extension files not found in source profile: {Path}", srcDir);
                }

                var srcExtensions = srcMacs?["extensions"] as JsonObject;
                var srcSettings = srcExtensions?["settings"] as JsonObject;
                var srcSettingsEncryptedHash = srcExtensions?["settings_encrypted_hash"] as JsonObject;

                if (srcSettings?[extId] != null)
                {
                    targetSettings[extId] = srcSettings[extId]?.DeepClone();
                    _logger.LogDebug("Copied MAC signature for extension: {ExtId}", extId);
                }
                if (srcSettingsEncryptedHash?[extId] != null)
                {
                    targetSettingsEncryptedHash[extId] = srcSettingsEncryptedHash[extId]?.DeepClone();
                    _logger.LogDebug("Copied encrypted hash MAC signature for extension: {ExtId}", extId);
                }
            }
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        if (!_fileSystem.DirectoryExists(sourceDir)) return;
        _fileSystem.CreateDirectory(targetDir);

        foreach (var file in _fileSystem.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(targetDir, relativePath);
            var destDir = Path.GetDirectoryName(destFile);
            if (destDir != null && !_fileSystem.DirectoryExists(destDir))
            {
                _fileSystem.CreateDirectory(destDir);
            }
            _fileSystem.CopyFile(file, destFile, true);
        }
    }

    private void ApplyTheme(JsonNode root, ThemeInformation theme)
    {
        var rootObj = root as JsonObject;
        if (rootObj == null) return;

        if (theme == null)
        {
            var browserThemeNode = root["browser"]?["theme"] as JsonObject;
            if (browserThemeNode != null)
            {
                browserThemeNode.Remove("color");
                if (browserThemeNode.Count == 0)
                {
                    (root["browser"] as JsonObject)?.Remove("theme");
                }
            }
            (root["profile"] as JsonObject)?.Remove("theme_color");

            SetJsonValue(root, new[] { "brave", "colors", "theme_mode" }, 0);

            var extensionsThemeNode = root["extensions"]?["theme"] as JsonObject;
            if (extensionsThemeNode != null)
            {
                extensionsThemeNode.Remove("id");
                if (extensionsThemeNode.Count == 0)
                {
                    (root["extensions"] as JsonObject)?.Remove("theme");
                }
            }
            RemoveWallpaperSettings(root);
            return;
        }

        if (theme.ThemeColor.HasValue)
        {
            SetJsonValue(root, new[] { "browser", "theme", "color" }, theme.ThemeColor.Value);
            SetJsonValue(root, new[] { "profile", "theme_color" }, theme.ThemeColor.Value);
        }
        else
        {
            var browserThemeNode = root["browser"]?["theme"] as JsonObject;
            if (browserThemeNode != null)
            {
                browserThemeNode.Remove("color");
                if (browserThemeNode.Count == 0)
                {
                    (root["browser"] as JsonObject)?.Remove("theme");
                }
            }
            (root["profile"] as JsonObject)?.Remove("theme_color");
        }

        if (theme.SystemThemeMode != null)
        {
            int modeInt = theme.SystemThemeMode switch
            {
                "System" => 0,
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };
            SetJsonValue(root, new[] { "brave", "colors", "theme_mode" }, modeInt);
        }

        if (theme.ThemeId != null)
        {
            SetJsonValue(root, new[] { "extensions", "theme", "id" }, theme.ThemeId);
        }
        else
        {
            var extensionsThemeNode = root["extensions"]?["theme"] as JsonObject;
            if (extensionsThemeNode != null)
            {
                extensionsThemeNode.Remove("id");
                if (extensionsThemeNode.Count == 0)
                {
                    (root["extensions"] as JsonObject)?.Remove("theme");
                }
            }
            if (!theme.ThemeColor.HasValue)
            {
                RemoveWallpaperSettings(root);
            }
        }
    }

    private void RemoveWallpaperSettings(JsonNode root)
    {
        (root["ntp"] as JsonObject)?.Remove("custom_background");

        var ntpNode = root["ntp"] as JsonObject;
        if (ntpNode != null && ntpNode.Count == 0)
        {
            (root as JsonObject)?.Remove("ntp");
        }

        var braveNewTabPage = root["brave"]?["new_tab_page"] as JsonObject;
        if (braveNewTabPage != null)
        {
            braveNewTabPage.Remove("background_image_source");
            braveNewTabPage.Remove("custom_background_source");
            if (braveNewTabPage.Count == 0)
            {
                (root["brave"] as JsonObject)?.Remove("new_tab_page");
            }
        }
    }

    private void ApplySearchEngine(JsonNode root, SearchEngineInformation search)
    {
        if (search.Name != null) SetJsonValue(root, new[] { "default_search_provider", "name" }, search.Name);
        if (search.Keyword != null) SetJsonValue(root, new[] { "default_search_provider", "keyword" }, search.Keyword);
        if (search.SearchUrl != null) SetJsonValue(root, new[] { "default_search_provider", "search_url" }, search.SearchUrl);

        if (search.Name == "Brave" || string.IsNullOrEmpty(search.Name))
        {
            (root as JsonObject)?.Remove("synced_default_search_provider_guid");
            (root as JsonObject)?.Remove("default_search_provider_data");
            (root as JsonObject)?.Remove("default_search_provider_data_signature");
        }
    }

    private void ApplySidebar(JsonNode root, SidebarConfiguration sidebar)
    {
        SetJsonValue(root, new[] { "brave", "sidebar", "show" }, sidebar.ShowSidebar);
        if (sidebar.Position != null) SetJsonValue(root, new[] { "brave", "sidebar", "position" }, sidebar.Position);
    }

    private void ApplyVerticalTabs(JsonNode root, VerticalTabsConfiguration tabs)
    {
        SetJsonValue(root, new[] { "brave", "tabs", "use_vertical_tabs" }, tabs.UseVerticalTabs);
    }

    private void ApplyExtensions(JsonNode root, IReadOnlyList<ExtensionInfo> extensions)
    {
        var settingsNode = root["extensions"]?["settings"];
        if (settingsNode == null)
        {
            SetJsonValue(root, new[] { "extensions", "settings" }, new JsonObject());
            settingsNode = root["extensions"]!["settings"]!;
        }

        var settingsObj = settingsNode as JsonObject;
        if (settingsObj == null) return;

        foreach (var ext in extensions)
        {
            var extNode = settingsObj[ext.Id];
            if (extNode == null)
            {
                var newExt = new JsonObject();
                settingsObj[ext.Id] = newExt;
                extNode = newExt;
            }

            var extObj = extNode as JsonObject;
            if (extObj == null) continue;

            var manifestNode = extObj["manifest"];
            if (manifestNode == null)
            {
                var newManifest = new JsonObject();
                extObj["manifest"] = newManifest;
                manifestNode = newManifest;
            }

            var manifestObj = manifestNode as JsonObject;
            if (manifestObj != null)
            {
                manifestObj["name"] = JsonValue.Create(ext.Name);
                manifestObj["version"] = JsonValue.Create(ext.Version);
            }
            extObj["state"] = JsonValue.Create(ext.IsEnabled ? 1 : 0);
        }
    }

    private void SetJsonValue<T>(JsonNode root, string[] path, T value)
    {
        JsonNode current = root;
        for (int i = 0; i < path.Length - 1; i++)
        {
            var key = path[i];
            var next = current[key];
            if (next == null)
            {
                var newObj = new JsonObject();
                var currentObj = current as JsonObject;
                if (currentObj != null)
                {
                    currentObj[key] = newObj;
                }
                next = newObj;
            }
            current = next;
        }

        var parentObj = current as JsonObject;
        if (parentObj != null)
        {
            if (value is JsonNode node)
            {
                parentObj[path[^1]] = node;
            }
            else
            {
                parentObj[path[^1]] = JsonValue.Create(value);
            }
        }
    }
}
