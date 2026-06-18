using System.Collections.Generic;

namespace Aevor.Core.Models;

public record ProfileValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
