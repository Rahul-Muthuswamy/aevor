using System.Text.Json;
using Aevor.Application.Interfaces;

namespace Aevor.Infrastructure.Services;

public class JsonPathTraverser
{
    private readonly IDiscoveredSettingRegistry _registry;
    private readonly string _browser;
    private readonly string _category;

    private static readonly HashSet<string> SimpleKnownPaths = new()
    {
        "profile.name",
        "extensions.theme.id",
        "browser.theme.color",
        "profile.theme_color",
        "brave.colors.theme_mode",
        "default_search_provider.name",
        "default_search_provider.keyword",
        "default_search_provider.search_url",
        "brave.sidebar.show",
        "brave.sidebar.position",
        "brave.tabs.use_vertical_tabs",
        "extensions.settings"
    };

    public JsonPathTraverser(IDiscoveredSettingRegistry registry, string browser, string category)
    {
        _registry = registry;
        _browser = browser;
        _category = category;
    }

    public void Traverse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            TraverseElement(document.RootElement, string.Empty);
        }
        catch
        {
        }
    }

    private void TraverseElement(JsonElement element, string currentPath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextPath = string.IsNullOrEmpty(currentPath) ? property.Name : $"{currentPath}.{property.Name}";
                    CheckAndRecord(nextPath, property.Value);
                    TraverseElement(property.Value, nextPath);
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var nextPath = $"{currentPath}[{index}]";
                    CheckAndRecord(nextPath, item);
                    TraverseElement(item, nextPath);
                    index++;
                }
                break;
        }
    }

    private void CheckAndRecord(string path, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.Array)
        {
            return;
        }

        if (!IsKnownPath(path))
        {
            _registry.RecordDiscoveredSetting(_browser, _category, path, value.ValueKind.ToString());
        }
    }

    private bool IsKnownPath(string path)
    {
        if (SimpleKnownPaths.Contains(path))
        {
            return true;
        }

        if (path.StartsWith("extensions.settings."))
        {
            var parts = path.Split('.');
            if (parts.Length == 5 && parts[3] == "manifest" && (parts[4] == "name" || parts[4] == "version"))
            {
                return true;
            }
            if (parts.Length == 4 && parts[3] == "state")
            {
                return true;
            }
        }

        return false;
    }
}
