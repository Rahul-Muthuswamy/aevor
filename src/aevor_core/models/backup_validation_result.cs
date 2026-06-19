using System.Collections.Generic;

namespace Aevor.Core.Models;

public record BackupValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings
);
