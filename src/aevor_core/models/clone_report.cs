using System.Collections.Generic;

namespace Aevor.Core.Models;

public record CloneReport(
    BraveProfile SourceProfile,
    BraveProfile DestinationProfile,
    IReadOnlyList<string> SettingsCopied,
    IReadOnlyList<string> ExtensionsCopied,
    IReadOnlyList<ExcludedArtifact> ExcludedArtifacts,
    IReadOnlyList<string> Warnings,
    ProfileValidationResult ValidationResult
);
