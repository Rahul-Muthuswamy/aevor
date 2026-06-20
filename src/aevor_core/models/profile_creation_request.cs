namespace Aevor.Core.Models;

public record ProfileCreationRequest(
    string ProfileName,
    string? FolderName = null,
    string? AvatarIcon = null
);
