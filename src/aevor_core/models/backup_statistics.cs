using System;

namespace Aevor.Core.Models;

public record BackupStatistics(
    int FileCount,
    long TotalSize,
    TimeSpan Duration
);
