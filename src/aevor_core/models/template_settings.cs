using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateSettings(
    ThemeInformation Theme,
    SearchEngineInformation SearchEngine,
    SidebarConfiguration Sidebar,
    VerticalTabsConfiguration VerticalTabs,
    IReadOnlyDictionary<string, object> BrowserPreferences
);
