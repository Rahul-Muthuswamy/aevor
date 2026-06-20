using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateApplicationPreview(
    IReadOnlyList<string> SettingsToModify,
    IReadOnlyList<string> ExtensionsToModify,
    IReadOnlyList<string> FilesAffected,
    IReadOnlyList<string> Warnings
);
