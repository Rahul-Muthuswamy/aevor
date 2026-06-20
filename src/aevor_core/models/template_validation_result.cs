using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateValidationResult(
    bool IsValid,
    IReadOnlyList<TemplateError> Errors,
    IReadOnlyList<TemplateWarning> Warnings
);
