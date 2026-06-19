using System;
using System.Collections.Generic;

namespace Aevor.Core.Models;

public record TemplateApplicationResult(
    bool IsSuccess,
    string? ErrorMessage,
    Guid? BackupId = null,
    IReadOnlyList<string>? AppliedChanges = null
);
