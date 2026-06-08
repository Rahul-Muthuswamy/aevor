namespace Aevor.Core.Models;

public record BraveProfile(
    string FolderName,
    string DisplayName,
    bool IsDefault,
    bool IsLastUsed,
    string ProfilePath
);
