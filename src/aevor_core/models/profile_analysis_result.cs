namespace Aevor.Core.Models;

public record ProfileAnalysisResult(
    string ProfileName,
    string ProfilePath,
    ThemeInformation Theme,
    SearchEngineInformation SearchEngine,
    SidebarConfiguration Sidebar,
    VerticalTabsConfiguration VerticalTabs,
    IReadOnlyList<ExtensionInfo> InstalledExtensions,
    int ExtensionCount,
    DateTime AnalysisTimestamp,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors
);
