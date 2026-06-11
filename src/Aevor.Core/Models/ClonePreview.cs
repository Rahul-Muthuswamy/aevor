using System.Collections.Generic;

namespace Aevor.Core.Models;

public record ClonePreview(
    string SourceProfileFolderName,
    string DestinationProfileName,
    IReadOnlyList<string> SettingsToCopy,
    IReadOnlyList<string> ExtensionsToCopy,
    IReadOnlyList<string> Warnings
);
