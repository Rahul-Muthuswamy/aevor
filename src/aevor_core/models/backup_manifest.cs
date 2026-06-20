using System;
using System.Text.Json.Serialization;

namespace Aevor.Core.Models;

public record BackupManifest(
    [property: JsonPropertyName("backupId")] Guid BackupId,
    [property: JsonPropertyName("createdAt")] DateTime CreatedAt,
    [property: JsonPropertyName("profileName")] string ProfileName,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("profileHash")] string ProfileHash,
    [property: JsonPropertyName("fileCount")] int FileCount,
    [property: JsonPropertyName("backupSize")] long BackupSize
);
