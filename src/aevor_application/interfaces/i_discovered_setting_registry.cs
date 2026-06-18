namespace Aevor.Application.Interfaces;

public interface IDiscoveredSettingRegistry
{
    void RecordDiscoveredSetting(string browser, string category, string jsonPath, string rawValueDescription);
    Task SaveAsync();
}
