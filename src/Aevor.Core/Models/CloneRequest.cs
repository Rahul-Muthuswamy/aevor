namespace Aevor.Core.Models;

public record CloneRequest(
    string SourceProfileFolderName,
    string DestinationProfileName,
    string? DestinationProfileFolderName = null
);
