namespace Aevor.Core.Models;

public record CloneResult(
    bool IsSuccess,
    string? ErrorMessage,
    CloneReport? Report = null
);
