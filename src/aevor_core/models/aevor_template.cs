using System.Collections.Generic;

namespace Aevor.Core.Models;

public record AevorTemplate(
    TemplateMetadata Metadata,
    TemplateSettings Settings,
    IReadOnlyList<ExtensionInfo> Extensions,
    TemplateAssets Assets,
    IReadOnlyList<TemplateWarning> Warnings,
    IReadOnlyList<ExcludedArtifact> ExcludedArtifacts
);
