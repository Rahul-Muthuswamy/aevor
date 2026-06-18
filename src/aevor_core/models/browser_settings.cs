namespace Aevor.Core.Models;

public record BrowserSettings(
    ThemeInformation Theme,
    SearchEngineInformation SearchEngine,
    SidebarConfiguration Sidebar,
    VerticalTabsConfiguration VerticalTabs,
    IReadOnlyList<ExtensionInfo> Extensions
);
