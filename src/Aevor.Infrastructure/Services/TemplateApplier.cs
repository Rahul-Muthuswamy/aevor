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
    private readonly ILogger<TemplateApplier> _logger;

    public TemplateApplier(
        IFileSystem fileSystem,
        ITemplateValidator templateValidator,
        IBackupService backupService,
        ILogger<TemplateApplier> logger)
    {
        _fileSystem = fileSystem;
        _templateValidator = templateValidator;
        _backupService = backupService;
        _logger = logger;
    }

    public async Task<TemplateApplicationResult> ApplyTemplateAsync(AevorTemplate template, BraveProfile profile, bool skipBackup = false)
    {
        if (template == null) throw new ArgumentNullException(nameof(template));
        if (profile == null) throw new ArgumentNullException(nameof(profile));

        _logger.LogInformation("Template application started for profile: {ProfileName}", profile.DisplayName);

        // 1. Validate template
        var templateVal = _templateValidator.Validate(template);
        if (!templateVal.IsValid)
        {
            var errorsStr = string.Join("; ", templateVal.Errors.Select(e => e.Message));
            _logger.LogError("Template application failed. Invalid template: {Errors}", errorsStr);
            return new TemplateApplicationResult(false, $"Invalid template: {errorsStr}");
        }

        // 2. Validate profile
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
            // 3. Automatically create backup before modification
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
            // 4. Modify Preferences
            var prefText = await _fileSystem.ReadAllTextAsync(prefPath);
            var prefRoot = JsonNode.Parse(prefText) ?? new JsonObject();

            // 5. Modify Secure Preferences
            var secPrefText = await _fileSystem.ReadAllTextAsync(secPrefPath);
            var secPrefRoot = JsonNode.Parse(secPrefText) ?? new JsonObject();

            // Apply Settings
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

            // Apply Extensions to both files
            if (template.Extensions != null && template.Extensions.Count > 0)
            {
                ApplyExtensions(prefRoot, template.Extensions);
                ApplyExtensions(secPrefRoot, template.Extensions);
                appliedChanges.Add($"{template.Extensions.Count} Extensions configuration applied.");
            }

            // Save back
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
                
                // Execute Rollback
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

        // Validate template version
        if (template.Metadata?.TemplateVersion == null || template.Metadata.TemplateVersion.ToString() != "1.0")
        {
            errors.Add($"Unsupported or missing template version: '{template.Metadata?.TemplateVersion?.ToString() ?? "null"}'");
        }

        // Validate profile existence
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

    private void ApplyTheme(JsonNode root, ThemeInformation theme)
    {
        if (theme == null)
        {
            var browserThemeNode = root["browser"]?["theme"]?.AsObject();
            if (browserThemeNode != null)
            {
                browserThemeNode.Remove("color");
                if (browserThemeNode.Count == 0)
                {
                    root["browser"]?.AsObject()?.Remove("theme");
                }
            }
            root["profile"]?.AsObject()?.Remove("theme_color");

            SetJsonValue(root, new[] { "brave", "colors", "theme_mode" }, 0);

            var extensionsThemeNode = root["extensions"]?["theme"]?.AsObject();
            if (extensionsThemeNode != null)
            {
                extensionsThemeNode.Remove("id");
                if (extensionsThemeNode.Count == 0)
                {
                    root["extensions"]?.AsObject()?.Remove("theme");
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
            var browserThemeNode = root["browser"]?["theme"]?.AsObject();
            if (browserThemeNode != null)
            {
                browserThemeNode.Remove("color");
                if (browserThemeNode.Count == 0)
                {
                    root["browser"]?.AsObject()?.Remove("theme");
                }
            }
            root["profile"]?.AsObject()?.Remove("theme_color");
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
            var extensionsThemeNode = root["extensions"]?["theme"]?.AsObject();
            if (extensionsThemeNode != null)
            {
                extensionsThemeNode.Remove("id");
                if (extensionsThemeNode.Count == 0)
                {
                    root["extensions"]?.AsObject()?.Remove("theme");
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
        root["ntp"]?.AsObject()?.Remove("custom_background");
        
        var ntpNode = root["ntp"]?.AsObject();
        if (ntpNode != null && ntpNode.Count == 0)
        {
            root.AsObject().Remove("ntp");
        }

        var braveNewTabPage = root["brave"]?["new_tab_page"]?.AsObject();
        if (braveNewTabPage != null)
        {
            braveNewTabPage.Remove("background_image_source");
            braveNewTabPage.Remove("custom_background_source");
            if (braveNewTabPage.Count == 0)
            {
                root["brave"]?.AsObject()?.Remove("new_tab_page");
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
            root.AsObject().Remove("synced_default_search_provider_guid");
            root.AsObject().Remove("default_search_provider_data");
            root.AsObject().Remove("default_search_provider_data_signature");
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

        var settingsObj = settingsNode.AsObject();
        foreach (var ext in extensions)
        {
            var extNode = settingsObj[ext.Id];
            if (extNode == null)
            {
                var newExt = new JsonObject();
                settingsObj[ext.Id] = newExt;
                extNode = newExt;
            }

            var manifestNode = extNode["manifest"];
            if (manifestNode == null)
            {
                var newManifest = new JsonObject();
                extNode.AsObject()["manifest"] = newManifest;
                manifestNode = newManifest;
            }

            manifestNode.AsObject()["name"] = JsonValue.Create(ext.Name);
            manifestNode.AsObject()["version"] = JsonValue.Create(ext.Version);
            extNode.AsObject()["state"] = JsonValue.Create(ext.IsEnabled ? 1 : 0);
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
                current.AsObject()[key] = newObj;
                next = newObj;
            }
            current = next;
        }

        if (value is JsonNode node)
        {
            current.AsObject()[path[^1]] = node;
        }
        else
        {
            current.AsObject()[path[^1]] = JsonValue.Create(value);
        }
    }
}
