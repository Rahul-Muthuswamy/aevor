using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateImportResult(
    AevorTemplate? Template,
    bool IsSuccess,
    IReadOnlyList<TemplateError> Errors,
    IReadOnlyList<TemplateWarning> Warnings
);
