using System.Text.Json;
using Aevor.Application.Interfaces;
using Aevor.Application.Models;

namespace Aevor.Infrastructure.Services;

public class DiscoveredSettingRegistry : IDiscoveredSettingRegistry
{
    private readonly IFileSystem _fileSystem;
    private readonly List<DiscoveredSetting> _deltaSettings = new();
    private readonly object _lock = new();

    public DiscoveredSettingRegistry(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void RecordDiscoveredSetting(string browser, string category, string jsonPath, string rawValueDescription)
    {
        lock (_lock)
        {
            var setting = new DiscoveredSetting(
                browser,
                category,
                jsonPath,
                rawValueDescription,
                DateTime.UtcNow
            );
            _deltaSettings.Add(setting);
        }
    }

    public async Task SaveAsync()
    {
        List<DiscoveredSetting> currentDelta;
        lock (_lock)
        {
            if (_deltaSettings.Count == 0)
            {
                return;
            }
            currentDelta = new List<DiscoveredSetting>(_deltaSettings);
            _deltaSettings.Clear();
        }

        var discoveryDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Aevor",
            "Discovery"
        );

        var discoveryFilePath = Path.Combine(discoveryDirectory, "discovered_settings.json");

        if (!_fileSystem.DirectoryExists(discoveryDirectory))
        {
            _fileSystem.CreateDirectory(discoveryDirectory);
        }

        var existingSettings = new List<DiscoveredSetting>();
        if (_fileSystem.FileExists(discoveryFilePath))
        {
            try
            {
                var content = await _fileSystem.ReadAllTextAsync(discoveryFilePath);
                var parsed = JsonSerializer.Deserialize<List<DiscoveredSetting>>(content);
                if (parsed != null)
                {
                    existingSettings.AddRange(parsed);
                }
            }
            catch
            {
            }
        }

        foreach (var newSetting in currentDelta)
        {
            var exists = existingSettings.Any(s =>
                s.Browser.Equals(newSetting.Browser, StringComparison.OrdinalIgnoreCase) &&
                s.Category.Equals(newSetting.Category, StringComparison.OrdinalIgnoreCase) &&
                s.JsonPath.Equals(newSetting.JsonPath, StringComparison.OrdinalIgnoreCase)
            );

            if (!exists)
            {
                existingSettings.Add(newSetting);
            }
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        var jsonContent = JsonSerializer.Serialize(existingSettings, jsonOptions);
        await _fileSystem.WriteAllTextAsync(discoveryFilePath, jsonContent);
    }
}
