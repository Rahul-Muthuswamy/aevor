namespace Aevor.Core.Models;

public record RestoreResult(
    bool IsSuccess,
    string? ErrorMessage,
    int FilesRestored,
    long TotalBytesRestored
);
