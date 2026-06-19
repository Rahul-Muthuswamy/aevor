using System.Collections.Generic;

namespace Aevor.Core.Models;

public record CloneValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
