namespace Aevor.Core.Models;

public record BackupResult(
    bool IsSuccess,
    BackupMetadata? Metadata,
    string? ErrorMessage,
    BackupStatistics? Statistics = null
);
