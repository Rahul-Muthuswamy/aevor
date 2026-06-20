using System;
using System.Text.Json.Serialization;

namespace Aevor.Core.Models;

public record BackupMetadata(
    [property: JsonPropertyName("backupId")] Guid BackupId,
    [property: JsonPropertyName("profileName")] string ProfileName,
    [property: JsonPropertyName("profilePath")] string ProfilePath,
    [property: JsonPropertyName("createdTimestamp")] DateTime CreatedTimestamp,
    [property: JsonPropertyName("backupSize")] long BackupSize,
    [property: JsonPropertyName("backupVersion")] string BackupVersion,
    [property: JsonPropertyName("status")] BackupStatus Status
);
