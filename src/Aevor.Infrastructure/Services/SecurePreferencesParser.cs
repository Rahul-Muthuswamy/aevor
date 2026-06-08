using System.Text.Json;
using System.Text.Json.Nodes;
using Aevor.Application.Interfaces;
using Aevor.Core.Exceptions;
using Aevor.Core.Models;

namespace Aevor.Infrastructure.Services;

public class SecurePreferencesParser : ISecurePreferencesParser
{
    private readonly IFileSystem _fileSystem;
    private readonly IDiscoveredSettingRegistry _registry;

    public SecurePreferencesParser(IFileSystem fileSystem, IDiscoveredSettingRegistry registry)
    {
        _fileSystem = fileSystem;
        _registry = registry;
    }

    public async Task<BrowserSettings> ParseAsync(string filePath)
    {
        if (!_fileSystem.FileExists(filePath))
        {
            throw new SecurePreferencesFileNotFoundException($"Secure Preferences file not found at: {filePath}");
        }

        string jsonContent;
        try
        {
            jsonContent = await _fileSystem.ReadAllTextAsync(filePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ProfileAccessDeniedException($"Access denied to secure preferences file: {filePath}", ex);
        }
        catch (IOException ex)
        {
            throw new CorruptedProfileException($"Failed to read secure preferences file: {filePath}. {ex.Message}");
        }

        JsonNode? rootNode;
        try
        {
            rootNode = JsonNode.Parse(jsonContent);
            if (rootNode == null)
            {
                throw new InvalidSecurePreferencesJsonException("Secure Preferences file is empty.", null!);
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidSecurePreferencesJsonException("Failed to parse Secure Preferences JSON.", ex);
        }

        var traverser = new JsonPathTraverser(_registry, "Brave", "SecurePreferences");
        traverser.Traverse(jsonContent);

        var themeId = rootNode["extensions"]?["theme"]?["id"]?.GetValue<string>();
        var systemThemeModeInt = rootNode["brave"]?["colors"]?["theme_mode"]?.GetValue<int>();
        var systemThemeMode = systemThemeModeInt switch
        {
            0 => "System",
            1 => "Light",
            2 => "Dark",
            _ => null
        };
        var themeColor = rootNode["browser"]?["theme"]?["color"]?.GetValue<int>()
            ?? rootNode["profile"]?["theme_color"]?.GetValue<int>();

        var themeInfo = new ThemeInformation(themeId, systemThemeMode, themeColor);

        var searchName = rootNode["default_search_provider"]?["name"]?.GetValue<string>();
        var searchKeyword = rootNode["default_search_provider"]?["keyword"]?.GetValue<string>();
        var searchUrl = rootNode["default_search_provider"]?["search_url"]?.GetValue<string>();

        var searchInfo = new SearchEngineInformation(searchName, searchKeyword, searchUrl);

        var showSidebar = rootNode["brave"]?["sidebar"]?["show"]?.GetValue<bool>() ?? false;
        var sidebarPositionVal = rootNode["brave"]?["sidebar"]?["position"]?.ToString();

        var sidebarInfo = new SidebarConfiguration(showSidebar, sidebarPositionVal);

        var useVerticalTabs = rootNode["brave"]?["tabs"]?["use_vertical_tabs"]?.GetValue<bool>() ?? false;

        var verticalTabsInfo = new VerticalTabsConfiguration(useVerticalTabs);

        var extensions = new List<ExtensionInfo>();
        var extensionsSettingsNode = rootNode["extensions"]?["settings"]?.AsObject();
        if (extensionsSettingsNode != null)
        {
            foreach (var property in extensionsSettingsNode)
            {
                var extensionId = property.Key;
                var settingsNode = property.Value;
                if (settingsNode == null)
                {
                    continue;
                }

                var name = settingsNode["manifest"]?["name"]?.GetValue<string>() ?? string.Empty;
                var version = settingsNode["manifest"]?["version"]?.GetValue<string>() ?? string.Empty;
                var stateVal = settingsNode["state"]?.GetValue<int>() ?? 0;
                var isEnabled = stateVal == 1;

                if (!string.IsNullOrEmpty(name))
                {
                    extensions.Add(new ExtensionInfo(extensionId, name, version, isEnabled));
                }
            }
        }

        return new BrowserSettings(
            themeInfo,
            searchInfo,
            sidebarInfo,
            verticalTabsInfo,
            extensions
        );
    }
}
