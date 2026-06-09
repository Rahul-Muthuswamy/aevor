using System;

namespace Aevor.Core.Models;

public record TemplateMetadata(
    string Name,
    string Description,
    DateTime CreatedTimestamp,
    TemplateVersion TemplateVersion,
    string SourceBrowser,
    string SourceBrowserVersion,
    string SourceProfileName,
    string GeneratorVersion
);
