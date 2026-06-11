using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateApplicationValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
